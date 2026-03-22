using Renci.SshNet;

namespace WinSTerm.Services;

public static class ConnectionFactory
{
    public static Renci.SshNet.ConnectionInfo Create(Models.ConnectionInfo info, string? plainPassword = null)
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
                    : "");
            authMethods.Add(new PasswordAuthenticationMethod(info.Username, password));
        }
        return new Renci.SshNet.ConnectionInfo(info.Host, info.Port, info.Username, authMethods.ToArray());
    }
}
