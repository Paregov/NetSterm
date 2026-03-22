using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace NetSterm.Services;

/// <summary>
/// Cross-platform encryption using AES-256-CBC with a machine-derived key.
/// Replaces Windows-only DPAPI (ProtectedData).
/// </summary>
public static class EncryptionService
{
    private static readonly byte[] s_salt = "NetSterm.v1.salt"u8.ToArray();

    /// <summary>
    /// Derives a machine+user-specific key from available environment info.
    /// Not as secure as DPAPI but works cross-platform.
    /// </summary>
    private static byte[] DeriveKey()
    {
        var material = $"{Environment.MachineName}:{Environment.UserName}:NetSterm";
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(material), s_salt, 100_000, HashAlgorithmName.SHA256, 32);
    }

    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        using var aes = Aes.Create();
        aes.Key = DeriveKey();
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Prepend IV to ciphertext
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    public static string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;

        try
        {
            var fullCipher = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = DeriveKey();

            var iv = new byte[aes.BlockSize / 8];
            var cipher = new byte[fullCipher.Length - iv.Length];
            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return string.Empty;
        }
    }
}
