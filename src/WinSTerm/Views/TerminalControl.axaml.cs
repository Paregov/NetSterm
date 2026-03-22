using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using WebViewCore.Events;
using WinSTerm.Services;
using WinSTerm.ViewModels;

namespace WinSTerm.Views;

public partial class TerminalControl : UserControl
{
    private SshConnectionService? _sshService;
    private bool _isWebViewReady;
    private bool _isSearchVisible;
    private bool _isAuthMode;
    private bool _authEchoOff;
    private string _authBuffer = "";
    private readonly DispatcherTimer _searchDebounceTimer;
    private readonly Uri _terminalHtmlUri;

    public TerminalControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;

        var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "terminal.html");
        _terminalHtmlUri = new Uri(htmlPath);

        _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _searchDebounceTimer.Tick += OnSearchDebounceTimerTick;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is SessionTabViewModel tab)
        {
            AttachSshService(tab.SshService);
            if (_isWebViewReady)
                tab.TerminalReady.TrySetResult(true);
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        TerminalWebView.WebViewCreated += OnWebViewCreated;
        TerminalWebView.NavigationCompleted += OnNavigationCompleted;
        TerminalWebView.WebMessageReceived += OnWebMessageReceived;

        TerminalWebView.Url = _terminalHtmlUri;

        if (DataContext is SessionTabViewModel tab)
        {
            AttachSshService(tab.SshService);
            if (_isWebViewReady)
                tab.TerminalReady.TrySetResult(true);
        }
    }

    private void OnWebViewCreated(object? sender, WebViewCreatedEventArgs e)
    {
        if (!e.IsSucceed)
        {
            System.Diagnostics.Debug.WriteLine($"WebView creation failed: {e.Message}");
        }
    }

    private void OnNavigationCompleted(object? sender, WebViewUrlLoadedEventArg e)
    {
        _isWebViewReady = true;

        if (DataContext is SessionTabViewModel tabVm)
            tabVm.TerminalReady.TrySetResult(true);

        Dispatcher.UIThread.Post(() =>
            TerminalWebView.ExecuteScriptAsync("window.terminalFocus()"));
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        DetachSshService();
        TerminalWebView.WebViewCreated -= OnWebViewCreated;
        TerminalWebView.NavigationCompleted -= OnNavigationCompleted;
        TerminalWebView.WebMessageReceived -= OnWebMessageReceived;
    }

    private void AttachSshService(SshConnectionService sshService)
    {
        DetachSshService();
        _sshService = sshService;
        _sshService.DataReceived += OnSshDataReceived;
        _sshService.Disconnected += OnSshDisconnected;
        _sshService.AuthPromptReceived += OnAuthPrompt;
    }

    private void DetachSshService()
    {
        if (_sshService != null)
        {
            _sshService.DataReceived -= OnSshDataReceived;
            _sshService.Disconnected -= OnSshDisconnected;
            _sshService.AuthPromptReceived -= OnAuthPrompt;
            _sshService = null;
        }
    }

    private void OnWebMessageReceived(object? sender, WebViewMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.Message;
            if (json == null) return;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();

            switch (type)
            {
                case "input":
                    var data = root.GetProperty("data").GetString();
                    if (data != null)
                    {
                        if (_isAuthMode)
                            HandleAuthInput(data);
                        else
                            _sshService?.SendData(data);
                    }
                    break;

                case "resize":
                    var cols = root.GetProperty("cols").GetUInt32();
                    var rows = root.GetProperty("rows").GetUInt32();
                    _sshService?.Resize(cols, rows);
                    break;

                case "cwd":
                    var cwdPath = root.GetProperty("path").GetString();
                    if (!string.IsNullOrEmpty(cwdPath))
                        _sshService?.UpdateCurrentDirectory(cwdPath);
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

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var json = JsonSerializer.Serialize(new { type = "output", data });
                TerminalWebView.PostWebMessageAsString(json, _terminalHtmlUri);
            }
            catch { /* WebView may be disposed */ }
        });
    }

    private void OnSshDisconnected()
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var msg = JsonSerializer.Serialize(new
                {
                    type = "output",
                    data = "\r\n\x1b[31m--- Connection closed ---\x1b[0m\r\n"
                });
                TerminalWebView.PostWebMessageAsString(msg, _terminalHtmlUri);
            }
            catch { }
        });
    }

    private void OnAuthPrompt(string promptText, bool isEchoOff)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _isAuthMode = true;
            _authEchoOff = isEchoOff;
            _authBuffer = "";
            WriteToTerminal(promptText);
        });
    }

    private void HandleAuthInput(string data)
    {
        foreach (var ch in data)
        {
            if (ch == '\r' || ch == '\n')
            {
                WriteToTerminal("\r\n");
                _isAuthMode = false;
                _sshService?.ProvideAuthResponse(_authBuffer);
                _authBuffer = "";
                return;
            }

            if (ch == '\x7f' || ch == '\b')
            {
                if (_authBuffer.Length > 0)
                {
                    _authBuffer = _authBuffer[..^1];
                    if (!_authEchoOff)
                        WriteToTerminal("\b \b");
                }
                continue;
            }

            if (ch < ' ')
                continue;

            _authBuffer += ch;
            if (!_authEchoOff)
                WriteToTerminal(ch.ToString());
        }
    }

    private void WriteToTerminal(string text)
    {
        if (!_isWebViewReady) return;

        try
        {
            var json = JsonSerializer.Serialize(new { type = "output", data = text });
            TerminalWebView.PostWebMessageAsString(json, _terminalHtmlUri);
        }
        catch { /* WebView may be disposed */ }
    }

    public async Task SearchAsync(string query)
    {
        if (_isWebViewReady)
            await TerminalWebView.ExecuteScriptAsync(
                $"window.terminalSearch({JsonSerializer.Serialize(query)})");
    }

    public async Task SearchNextAsync()
    {
        if (_isWebViewReady)
            await TerminalWebView.ExecuteScriptAsync("window.terminalSearchNext()");
    }

    public async Task SearchPreviousAsync()
    {
        if (_isWebViewReady)
            await TerminalWebView.ExecuteScriptAsync("window.terminalSearchPrevious()");
    }

    public async Task ClearTerminalAsync()
    {
        if (_isWebViewReady)
            await TerminalWebView.ExecuteScriptAsync("window.terminalClear()");
    }

    // --- Search overlay ---

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ShowSearch();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && _isSearchVisible)
        {
            HideSearch();
            e.Handled = true;
        }
    }

    private void ShowSearch()
    {
        _isSearchVisible = true;
        SearchBar.IsVisible = true;
        SearchTextBox.Focus();
        SearchTextBox.SelectAll();
    }

    private async void HideSearch()
    {
        _isSearchVisible = false;
        SearchBar.IsVisible = false;
        _searchDebounceTimer.Stop();

        try
        {
            if (_isWebViewReady)
                await TerminalWebView.ExecuteScriptAsync("window.terminalFocus()");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Hide search error: {ex.Message}");
        }
    }

    private void SearchTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private async void OnSearchDebounceTimerTick(object? sender, EventArgs e)
    {
        _searchDebounceTimer.Stop();
        var query = SearchTextBox.Text;
        try
        {
            if (!string.IsNullOrEmpty(query))
                await SearchAsync(query);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Search debounce error: {ex.Message}");
        }
    }

    private async void SearchTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            try
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    await SearchPreviousAsync();
                else
                    await SearchNextAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Search error: {ex.Message}");
            }
            e.Handled = true;
        }
    }

    private async void PreviousMatchButton_Click(object? sender, RoutedEventArgs e)
    {
        try { await SearchPreviousAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Previous match error: {ex.Message}"); }
    }

    private async void NextMatchButton_Click(object? sender, RoutedEventArgs e)
    {
        try { await SearchNextAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Next match error: {ex.Message}"); }
    }

    private void CloseSearchButton_Click(object? sender, RoutedEventArgs e)
    {
        HideSearch();
    }
}
