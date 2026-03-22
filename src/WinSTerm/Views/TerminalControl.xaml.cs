using Microsoft.Web.WebView2.Core;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using WinSTerm.Services;

namespace WinSTerm.Views;

public partial class TerminalControl : UserControl
{
    private SshConnectionService? _sshService;
    private bool _isWebViewReady;

    public TerminalControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await TerminalWebView.EnsureCoreWebView2Async();
            TerminalWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "terminal.html");
            TerminalWebView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);

            TerminalWebView.CoreWebView2.NavigationCompleted += (s, args) =>
            {
                _isWebViewReady = true;
                Dispatcher.BeginInvoke(() =>
                    TerminalWebView.CoreWebView2.ExecuteScriptAsync("window.terminalFocus()"));
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView2 init error: {ex.Message}");
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachSshService();
    }

    public void AttachSshService(SshConnectionService sshService)
    {
        DetachSshService();
        _sshService = sshService;
        _sshService.DataReceived += OnSshDataReceived;
        _sshService.Disconnected += OnSshDisconnected;
    }

    private void DetachSshService()
    {
        if (_sshService != null)
        {
            _sshService.DataReceived -= OnSshDataReceived;
            _sshService.Disconnected -= OnSshDisconnected;
            _sshService = null;
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.TryGetWebMessageAsString();
            if (json == null) return;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();

            switch (type)
            {
                case "input":
                    var data = root.GetProperty("data").GetString();
                    if (data != null)
                        _sshService?.SendData(data);
                    break;

                case "resize":
                    var cols = root.GetProperty("cols").GetUInt32();
                    var rows = root.GetProperty("rows").GetUInt32();
                    _sshService?.Resize(cols, rows);
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Message error: {ex.Message}");
        }
    }

    private void OnSshDataReceived(string data)
    {
        if (!_isWebViewReady) return;

        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                var json = JsonSerializer.Serialize(new { type = "output", data });
                TerminalWebView.CoreWebView2.PostWebMessageAsString(json);
            }
            catch { /* WebView may be disposed */ }
        });
    }

    private void OnSshDisconnected()
    {
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                var msg = JsonSerializer.Serialize(new { type = "output", data = "\r\n\x1b[31m--- Connection closed ---\x1b[0m\r\n" });
                TerminalWebView.CoreWebView2.PostWebMessageAsString(msg);
            }
            catch { }
        });
    }

    public async Task SearchAsync(string query)
    {
        if (_isWebViewReady)
            await TerminalWebView.CoreWebView2.ExecuteScriptAsync($"window.terminalSearch({JsonSerializer.Serialize(query)})");
    }

    public async Task SearchNextAsync()
    {
        if (_isWebViewReady)
            await TerminalWebView.CoreWebView2.ExecuteScriptAsync("window.terminalSearchNext()");
    }

    public async Task SearchPreviousAsync()
    {
        if (_isWebViewReady)
            await TerminalWebView.CoreWebView2.ExecuteScriptAsync("window.terminalSearchPrevious()");
    }

    public async Task ClearTerminalAsync()
    {
        if (_isWebViewReady)
            await TerminalWebView.CoreWebView2.ExecuteScriptAsync("window.terminalClear()");
    }
}
