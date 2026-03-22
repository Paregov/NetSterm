using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinSTerm.Models;
using WinSTerm.Services;

namespace WinSTerm.ViewModels;

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
            ShowHiddenFiles = ShowHiddenFiles
        };

        SettingsService.Instance.Apply(settings);

        window.DialogResult = true;
        window.Close();
    }

    [RelayCommand]
    private void Cancel(Window window)
    {
        window.DialogResult = false;
        window.Close();
    }

    [RelayCommand]
    private void BrowseLocalDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Default SFTP Local Directory",
            InitialDirectory = DefaultLocalDirectory
        };

        if (dialog.ShowDialog() == true)
        {
            DefaultLocalDirectory = dialog.FolderName;
        }
    }
}
