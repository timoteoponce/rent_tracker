using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace RentTracker.Web.Helpers;

public class EncryptionHelper
{
    private readonly IConfiguration _configuration;
    private byte[]? _key;

    public EncryptionHelper(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private byte[] GetKey()
    {
        if (_key != null)
            return _key;

        var keyString = _configuration["WhatsApp:EncryptionKey"] ?? string.Empty;
        if (string.IsNullOrEmpty(keyString))
        {
            throw new InvalidOperationException(
                "Configuration 'WhatsApp:EncryptionKey' is missing. " +
                "Add a 64-character hex key to appsettings.json under 'WhatsApp:EncryptionKey'. " +
                "You can generate one with: dotnet run --project RentTracker.csproj -- --generate-key (or call EncryptionHelper.GenerateKey() from code).");
        }

        using var sha256 = SHA256.Create();
        _key = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyString));
        return _key;
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

        var key = GetKey();

        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = new byte[aes.IV.Length + encryptedBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);
        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;

        var key = GetKey();

        var cipherBytes = Convert.FromBase64String(cipherText);
        if (cipherBytes.Length < 16)
            throw new InvalidOperationException("Invalid ciphertext.");

        var iv = new byte[16];
        Buffer.BlockCopy(cipherBytes, 0, iv, 0, 16);
        var encryptedBytes = new byte[cipherBytes.Length - 16];
        Buffer.BlockCopy(cipherBytes, 16, encryptedBytes, 0, encryptedBytes.Length);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
        return Encoding.UTF8.GetString(decryptedBytes);
    }
}
