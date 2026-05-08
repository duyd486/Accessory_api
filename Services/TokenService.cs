using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Vibra_Dotnet_api.Models;

namespace Vibra_Dotnet_api.Services;

public interface ITokenService
{
    string CreateToken(User user);
}

public sealed class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string CreateToken(User user)
    {
        var secret = _configuration["Jwt:Secret"] ?? "dev-secret-change-me";
        var key = JwtKeyProvider.CreateSigningKey(secret);
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Default to normal user when role is NULL
        var role = user.Role ?? 1;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new("role", role.ToString())
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
