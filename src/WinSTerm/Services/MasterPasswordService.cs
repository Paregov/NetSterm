using System.Security.Cryptography;
using System.Text;

namespace WinSTerm.Services;

public static class MasterPasswordService
{
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int Iterations = 100_000;

    public static bool IsUnlocked { get; private set; }

    public static bool IsEnabled =>
        SettingsService.Instance.Current.IsMasterPasswordEnabled
        && !string.IsNullOrEmpty(SettingsService.Instance.Current.MasterPasswordHash);

    public static bool Verify(string password)
    {
        var settings = SettingsService.Instance.Current;
        if (!settings.IsMasterPasswordEnabled
            || string.IsNullOrEmpty(settings.MasterPasswordHash))
        {
            IsUnlocked = true;
            return true;
        }

        var salt = Convert.FromBase64String(settings.MasterPasswordSalt ?? "");
        var hash = HashPassword(password, salt);

        if (CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(hash),
                Convert.FromBase64String(settings.MasterPasswordHash)))
        {
            IsUnlocked = true;
            return true;
        }

        return false;
    }

    public static void SetPassword(string newPassword)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = HashPassword(newPassword, salt);

        var settings = SettingsService.Instance.Current;
        settings.IsMasterPasswordEnabled = true;
        settings.MasterPasswordHash = hash;
        settings.MasterPasswordSalt = Convert.ToBase64String(salt);
        SettingsService.Instance.Save();
        IsUnlocked = true;
    }

    public static bool RemovePassword(string currentPassword)
    {
        if (!Verify(currentPassword))
            return false;

        var settings = SettingsService.Instance.Current;
        settings.IsMasterPasswordEnabled = false;
        settings.MasterPasswordHash = null;
        settings.MasterPasswordSalt = null;
        SettingsService.Instance.Save();
        return true;
    }

    public static bool ChangePassword(string currentPassword, string newPassword)
    {
        if (!Verify(currentPassword))
            return false;

        SetPassword(newPassword);
        return true;
    }

    public static void UnlockWithoutPassword()
    {
        IsUnlocked = true;
    }

    private static string HashPassword(string password, byte[] salt)
    {
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashBytes);
        return Convert.ToBase64String(hash);
    }
}
