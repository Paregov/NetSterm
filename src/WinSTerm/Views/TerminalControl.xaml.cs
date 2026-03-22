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
            AttachSshService(tab.SshService);
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
            AttachSshService(tab.SshService);
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

        if (_isWebViewReady)
            await TerminalWebView.CoreWebView2.ExecuteScriptAsync("window.terminalFocus()");
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
        if (!string.IsNullOrEmpty(query))
            await SearchAsync(query);
    }

    private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                _ = SearchPreviousAsync();
            else
                _ = SearchNextAsync();

            e.Handled = true;
        }
    }

    private async void PreviousMatchButton_Click(object sender, RoutedEventArgs e)
    {
        await SearchPreviousAsync();
    }

    private async void NextMatchButton_Click(object sender, RoutedEventArgs e)
    {
        await SearchNextAsync();
    }

    private void CloseSearchButton_Click(object sender, RoutedEventArgs e)
    {
        HideSearch();
    }
}
