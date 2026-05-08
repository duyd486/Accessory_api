using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

namespace Accessory_api.Services;

public static class JwtKeyProvider
{
    public static SymmetricSecurityKey CreateSigningKey(string? secret)
    {
        var raw = secret ?? string.Empty;
        var bytes = Encoding.UTF8.GetBytes(raw);

        // HS256 requires >= 256-bit key. If user provides a shorter secret (common in dev),
        // derive a fixed 256-bit key using SHA256 to avoid runtime failures.
        if (bytes.Length < 32)
        {
            bytes = SHA256.HashData(bytes);
        }

        return new SymmetricSecurityKey(bytes);
    }
}
