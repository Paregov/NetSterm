using Renci.SshNet;
using Serilog;

namespace WinSTerm.Services;

public static class ConnectionFactory
{
    public static Renci.SshNet.ConnectionInfo Create(Models.ConnectionInfo info, string? plainPassword = null)
    {
        return Create(info, plainPassword, null);
    }

    public static Renci.SshNet.ConnectionInfo Create(
        Models.ConnectionInfo info,
        string? plainPassword,
        Action<KeyboardInteractiveAuthenticationMethod>? configureKeyboardInteractive)
    {
        var authMethods = new List<AuthenticationMethod>();

        Log.Debug("Creating SSH connection for {User}@{Host}:{Port}", info.Username, info.Host, info.Port);

        if (info.AuthMethod == Models.AuthMethod.PrivateKey && !string.IsNullOrEmpty(info.PrivateKeyPath))
        {
            try
            {
                var keyFile = new PrivateKeyFile(info.PrivateKeyPath);
                authMethods.Add(new PrivateKeyAuthenticationMethod(info.Username, keyFile));
                Log.Debug("Auth method: PrivateKey ({Path})", info.PrivateKeyPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load private key from {Path}", info.PrivateKeyPath);
                throw;
            }
        }
        else
        {
            var password = plainPassword
                ?? (!string.IsNullOrEmpty(info.EncryptedPassword)
                    ? ConnectionStorageService.DecryptPassword(info.EncryptedPassword)
                    : null);

            if (password != null)
            {
                authMethods.Add(new PasswordAuthenticationMethod(info.Username, password));
                Log.Debug("Auth method: Password");
            }
            else if (configureKeyboardInteractive != null)
            {
                // Add both methods so SSH.NET can match whichever the server supports.
                // Keyboard-interactive prompts the user in the terminal.
                // Password with empty string is a placeholder — SshConnectionService
                // will catch the auth failure and retry with the real password.
                var kbdInteractive = new KeyboardInteractiveAuthenticationMethod(info.Username);
                configureKeyboardInteractive(kbdInteractive);
                authMethods.Add(kbdInteractive);
                authMethods.Add(new PasswordAuthenticationMethod(info.Username, ""));
                Log.Debug("Auth methods: KeyboardInteractive + Password (placeholder)");
            }
            else
            {
                authMethods.Add(new PasswordAuthenticationMethod(info.Username, ""));
                Log.Debug("Auth method: Password (empty placeholder)");
            }
        }

        Log.Debug("SSH ConnectionInfo created with {Count} auth method(s)", authMethods.Count);
        return new Renci.SshNet.ConnectionInfo(info.Host, info.Port, info.Username, authMethods.ToArray());
    }
}
