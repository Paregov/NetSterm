using Microsoft.Web.WebView2.Core;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
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

    public TerminalControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;

        _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _searchDebounceTimer.Tick += OnSearchDebounceTimerTick;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is SessionTabViewModel tab)
        {
            AttachSshService(tab.SshService);
            if (_isWebViewReady)
                tab.TerminalReady.TrySetResult(true);
        }
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

                if (DataContext is SessionTabViewModel tabVm)
                    tabVm.TerminalReady.TrySetResult(true);

                // TODO: Apply terminal settings (font, fontSize, scrollback, cursor) from
                // SettingsService.Instance.Current here by sending a JSON message to xterm.js.
                // Also subscribe to SettingsService.Instance.SettingsChanged to apply changes
                // at runtime when the user modifies settings. This requires adding a
                // 'applySettings' message handler in terminal.html.

                Dispatcher.BeginInvoke(() =>
                    TerminalWebView.CoreWebView2.ExecuteScriptAsync("window.terminalFocus()"));
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView2 init error: {ex.Message}");
        }

        // Wire up if DataContext is already set when we load
        if (DataContext is SessionTabViewModel tab)
        {
            AttachSshService(tab.SshService);
            if (_isWebViewReady)
                tab.TerminalReady.TrySetResult(true);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachSshService();
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
                if (TerminalWebView.CoreWebView2 == null) return;
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
                if (TerminalWebView.CoreWebView2 == null) return;
                var msg = JsonSerializer.Serialize(new { type = "output", data = "\r\n\x1b[31m--- Connection closed ---\x1b[0m\r\n" });
                TerminalWebView.CoreWebView2.PostWebMessageAsString(msg);
            }
            catch { }
        });
    }

    private void OnAuthPrompt(string promptText, bool isEchoOff)
    {
        Dispatcher.BeginInvoke(() =>
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
        if (!_isWebViewReady || TerminalWebView.CoreWebView2 == null) return;

        try
        {
            var json = JsonSerializer.Serialize(new { type = "output", data = text });
            TerminalWebView.CoreWebView2.PostWebMessageAsString(json);
        }
        catch { /* WebView may be disposed */ }
    }

    public async Task SearchAsync(string query)
    {
        if (_isWebViewReady && TerminalWebView.CoreWebView2 != null)
            await TerminalWebView.CoreWebView2.ExecuteScriptAsync($"window.terminalSearch({JsonSerializer.Serialize(query)})");
    }

    public async Task SearchNextAsync()
    {
        if (_isWebViewReady && TerminalWebView.CoreWebView2 != null)
            await TerminalWebView.CoreWebView2.ExecuteScriptAsync("window.terminalSearchNext()");
    }

    public async Task SearchPreviousAsync()
    {
        if (_isWebViewReady && TerminalWebView.CoreWebView2 != null)
            await TerminalWebView.CoreWebView2.ExecuteScriptAsync("window.terminalSearchPrevious()");
    }

    public async Task ClearTerminalAsync()
    {
        if (_isWebViewReady && TerminalWebView.CoreWebView2 != null)
            await TerminalWebView.CoreWebView2.ExecuteScriptAsync("window.terminalClear()");
    }

    // --- Search overlay ---

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
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
        SearchBar.Visibility = Visibility.Visible;
        SearchTextBox.Focus();
        SearchTextBox.SelectAll();
    }

    private async void HideSearch()
    {
        _isSearchVisible = false;
        SearchBar.Visibility = Visibility.Collapsed;
        _searchDebounceTimer.Stop();

        try
        {
            if (_isWebViewReady && TerminalWebView.CoreWebView2 != null)
                await TerminalWebView.CoreWebView2.ExecuteScriptAsync("window.terminalFocus()");
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Hide search error: {ex.Message}"); }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
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
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Search debounce error: {ex.Message}"); }
    }

    private async void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            try
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                    await SearchPreviousAsync();
                else
                    await SearchNextAsync();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Search error: {ex.Message}"); }
            e.Handled = true;
        }
    }

    private async void PreviousMatchButton_Click(object sender, RoutedEventArgs e)
    {
        try { await SearchPreviousAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Previous match error: {ex.Message}"); }
    }

    private async void NextMatchButton_Click(object sender, RoutedEventArgs e)
    {
        try { await SearchNextAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Next match error: {ex.Message}"); }
    }

    private void CloseSearchButton_Click(object sender, RoutedEventArgs e)
    {
        HideSearch();
    }
}
