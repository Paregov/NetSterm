using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetSterm.Models;
using NetSterm.Services;

namespace NetSterm.ViewModels;

public partial class SettingsDialogViewModel : ObservableObject
{
    // Terminal
    [ObservableProperty] private string _fontFamily;
    [ObservableProperty] private int _fontSize;
    [ObservableProperty] private int _scrollbackLines;
    [ObservableProperty] private string _cursorStyle;
    [ObservableProperty] private bool _cursorBlink;

    // SSH Defaults
    [ObservableProperty] private int _defaultKeepAliveSeconds;
    [ObservableProperty] private int _connectionTimeoutSeconds;
    [ObservableProperty] private bool _defaultCompression;

    // Appearance
    [ObservableProperty] private bool _showStatusBar;
    [ObservableProperty] private bool _confirmOnCloseTab;
    [ObservableProperty] private bool _confirmOnExit;

    // SFTP
    [ObservableProperty] private string _defaultLocalDirectory;
    [ObservableProperty] private bool _showHiddenFiles;

    // Security
    [ObservableProperty] private bool _isMasterPasswordEnabled;

    public List<string> FontFamilies { get; } =
    [
        "Cascadia Code",
        "Consolas",
        "Courier New",
        "Lucida Console",
        "Source Code Pro"
    ];

    public List<string> CursorStyles { get; } =
    [
        "block",
        "underline",
        "bar"
    ];

    public SettingsDialogViewModel()
    {
        var settings = SettingsService.Instance.Current;

        _fontFamily = settings.FontFamily;
        _fontSize = settings.FontSize;
        _scrollbackLines = settings.ScrollbackLines;
        _cursorStyle = settings.CursorStyle;
        _cursorBlink = settings.CursorBlink;

        _defaultKeepAliveSeconds = settings.DefaultKeepAliveSeconds;
        _connectionTimeoutSeconds = settings.ConnectionTimeoutSeconds;
        _defaultCompression = settings.DefaultCompression;

        _showStatusBar = settings.ShowStatusBar;
        _confirmOnCloseTab = settings.ConfirmOnCloseTab;
        _confirmOnExit = settings.ConfirmOnExit;

        _defaultLocalDirectory = settings.DefaultLocalDirectory;
        _showHiddenFiles = settings.ShowHiddenFiles;

        _isMasterPasswordEnabled = settings.IsMasterPasswordEnabled;
    }

    [RelayCommand]
    private void Save(Window window)
    {
        var settings = new AppSettings
        {
            FontFamily = FontFamily,
            FontSize = FontSize,
            ScrollbackLines = ScrollbackLines,
            CursorStyle = CursorStyle,
            CursorBlink = CursorBlink,
            DefaultKeepAliveSeconds = DefaultKeepAliveSeconds,
            ConnectionTimeoutSeconds = ConnectionTimeoutSeconds,
            DefaultCompression = DefaultCompression,
            ShowStatusBar = ShowStatusBar,
            ConfirmOnCloseTab = ConfirmOnCloseTab,
            ConfirmOnExit = ConfirmOnExit,
            DefaultLocalDirectory = DefaultLocalDirectory,
            ShowHiddenFiles = ShowHiddenFiles,

            // Security: hash and salt are managed by MasterPasswordService, preserve from current
            IsMasterPasswordEnabled = IsMasterPasswordEnabled,
            MasterPasswordHash = SettingsService.Instance.Current.MasterPasswordHash,
            MasterPasswordSalt = SettingsService.Instance.Current.MasterPasswordSalt
        };

        SettingsService.Instance.Apply(settings);

        // TODO: Avalonia migration - use window.Close(result) pattern for dialog result
        window.Close();
    }

    [RelayCommand]
#pragma warning disable CA1822 // Method is bound via [RelayCommand] and must remain instance
    private void Cancel(Window window)
    {
        window.Close();
    }
#pragma warning restore CA1822

    [RelayCommand]
#pragma warning disable CA1822 // Method is bound via [RelayCommand] and must remain instance
    private void BrowseLocalDirectory()
    {
        // TODO: Avalonia migration - Use Avalonia folder picker (StorageProvider API)
        // var dialog = new OpenFolderDialog { ... };
    }
#pragma warning restore CA1822
}
