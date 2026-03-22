using System.ComponentModel.DataAnnotations;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WinSTerm.Models;
using WinSTerm.Services;

namespace WinSTerm.ViewModels;

public partial class ConnectionDialogViewModel : ObservableValidator
{
    // --- General tab ---

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _description = "";

    [ObservableProperty]
    [Required(ErrorMessage = "Host is required")]
    [NotifyDataErrorInfo]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _host = "";

    [ObservableProperty]
    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    [NotifyDataErrorInfo]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private int _port = 22;

    [ObservableProperty]
    [Required(ErrorMessage = "Username is required")]
    [NotifyDataErrorInfo]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _username = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPasswordAuth))]
    [NotifyPropertyChangedFor(nameof(IsPrivateKeyAuth))]
    private AuthMethod _selectedAuthMethod = AuthMethod.Password;

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _privateKeyPath = "";

    [ObservableProperty]
    private bool _isEditMode;

    // --- Terminal tab ---

    [ObservableProperty]
    private string _terminalType = "xterm-256color";

    [ObservableProperty]
    private string _startupCommand = "";

    [ObservableProperty]
    private string _remoteDirectory = "";

    // --- Network tab ---

    [ObservableProperty]
    private int _keepAliveInterval = 30;

    [ObservableProperty]
    private bool _enableCompression;

    [ObservableProperty]
    private string _jumpHost = "";

    [ObservableProperty]
    [Range(1, 65535)]
    [NotifyDataErrorInfo]
    private int _jumpPort = 22;

    [ObservableProperty]
    private string _jumpUsername = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProxyEnabled))]
    private ProxyType _selectedProxyType = ProxyType.None;

    [ObservableProperty]
    private string _proxyHost = "";

    [ObservableProperty]
    [Range(1, 65535)]
    [NotifyDataErrorInfo]
    private int _proxyPort = 1080;

    private readonly string _connectionId;
    private readonly DateTime _createdAt;
    private string? _folderId;

    public bool IsPasswordAuth => SelectedAuthMethod == AuthMethod.Password;
    public bool IsPrivateKeyAuth => SelectedAuthMethod != AuthMethod.Password;
    public bool IsProxyEnabled => SelectedProxyType != ProxyType.None;

    public List<string> TerminalTypes { get; } =
    [
        "xterm-256color",
        "xterm",
        "vt100",
        "vt220",
        "ansi"
    ];

    public ConnectionInfo? Result { get; private set; }

    public ConnectionDialogViewModel() : this(null) { }

    public ConnectionDialogViewModel(ConnectionInfo? existing)
    {
        if (existing is not null)
        {
            IsEditMode = true;
            _connectionId = existing.Id;
            _createdAt = existing.CreatedAt;
            _folderId = existing.FolderId;

            // General
            Name = existing.Name;
            Description = existing.Description ?? "";
            Host = existing.Host;
            Port = existing.Port;
            Username = existing.Username;
            SelectedAuthMethod = existing.AuthMethod;
            PrivateKeyPath = existing.PrivateKeyPath ?? "";

            if (!string.IsNullOrEmpty(existing.EncryptedPassword))
            {
                try { Password = ConnectionStorageService.DecryptPassword(existing.EncryptedPassword); }
                catch { Password = ""; }
            }

            // Terminal
            TerminalType = existing.TerminalType ?? "xterm-256color";
            StartupCommand = existing.StartupCommand ?? "";
            RemoteDirectory = existing.RemoteDirectory ?? "";

            // Network
            KeepAliveInterval = existing.KeepAliveInterval;
            EnableCompression = existing.EnableCompression;
            JumpHost = existing.JumpHost ?? "";
            JumpPort = existing.JumpPort;
            JumpUsername = existing.JumpUsername ?? "";
            SelectedProxyType = existing.ProxyType;
            ProxyHost = existing.ProxyHost ?? "";
            ProxyPort = existing.ProxyPort;
        }
        else
        {
            _connectionId = Guid.NewGuid().ToString();
            _createdAt = DateTime.UtcNow;
        }
    }

    [RelayCommand]
    private void BrowseKey()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Private Key File",
            Filter = "Key Files (*.pem;*.ppk;*.key)|*.pem;*.ppk;*.key|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            PrivateKeyPath = dialog.FileName;
        }
    }

    private bool CanSave()
    {
        return !string.IsNullOrWhiteSpace(Host)
            && !string.IsNullOrWhiteSpace(Username)
            && Port is >= 1 and <= 65535;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save(Window window)
    {
        ValidateAllProperties();
        if (HasErrors) return;

        Result = new ConnectionInfo
        {
            Id = _connectionId,
            Name = string.IsNullOrWhiteSpace(Name) ? $"{Username}@{Host}" : Name,
            Host = Host.Trim(),
            Port = Port,
            Username = Username.Trim(),
            AuthMethod = SelectedAuthMethod,
            PrivateKeyPath = IsPrivateKeyAuth ? PrivateKeyPath : null,
            FolderId = _folderId,
            EncryptedPassword = !string.IsNullOrEmpty(Password)
                ? ConnectionStorageService.EncryptPassword(Password)
                : null,
            CreatedAt = _createdAt,
            LastConnectedAt = default,

            // Terminal
            TerminalType = TerminalType,
            StartupCommand = NullIfEmpty(StartupCommand),
            RemoteDirectory = NullIfEmpty(RemoteDirectory),

            // Network
            KeepAliveInterval = KeepAliveInterval,
            EnableCompression = EnableCompression,
            JumpHost = NullIfEmpty(JumpHost),
            JumpPort = JumpPort,
            JumpUsername = NullIfEmpty(JumpUsername),
            ProxyType = SelectedProxyType,
            ProxyHost = IsProxyEnabled ? NullIfEmpty(ProxyHost) : null,
            ProxyPort = ProxyPort,

            // Metadata
            Description = NullIfEmpty(Description)
        };

        window.DialogResult = true;
        window.Close();
    }

    [RelayCommand]
    private void Cancel(Window window)
    {
        window.DialogResult = false;
        window.Close();
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
