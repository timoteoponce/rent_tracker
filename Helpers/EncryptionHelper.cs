using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace RentTracker.Web.Helpers;

public class EncryptionHelper
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public EncryptionHelper(IConfiguration configuration)
    {
        var keyString = configuration["WhatsApp:EncryptionKey"] ?? string.Empty;
        if (string.IsNullOrEmpty(keyString))
        {
            keyString = GenerateKey();
        }

        using var sha256 = SHA256.Create();
        _key = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyString));
        _iv = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(keyString + "iv")).Take(16).ToArray();
    }

    public static string GenerateKey()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes);
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return Convert.ToBase64String(encryptedBytes);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var cipherBytes = Convert.FromBase64String(cipherText);
        var decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(decryptedBytes);
    }
}