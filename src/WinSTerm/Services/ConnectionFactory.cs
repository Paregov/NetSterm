using Renci.SshNet;

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

        if (info.AuthMethod == Models.AuthMethod.PrivateKey && !string.IsNullOrEmpty(info.PrivateKeyPath))
        {
            var keyFile = new PrivateKeyFile(info.PrivateKeyPath);
            authMethods.Add(new PrivateKeyAuthenticationMethod(info.Username, keyFile));
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
            }
            else
            {
                authMethods.Add(new PasswordAuthenticationMethod(info.Username, ""));
            }
        }

        return new Renci.SshNet.ConnectionInfo(info.Host, info.Port, info.Username, authMethods.ToArray());
    }
}
