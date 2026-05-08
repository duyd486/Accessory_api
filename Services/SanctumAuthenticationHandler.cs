using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Vibra_Dotnet_api.Data;

namespace Vibra_Dotnet_api.Services;

public sealed class SanctumAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly AppDbContext _db;

    public SanctumAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext db)
        : base(options, logger, encoder)
    {
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = header["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token) || !token.Contains('|'))
        {
            return AuthenticateResult.NoResult();
        }

        // Sanctum plainTextToken format: `{id}|{token}`
        var parts = token.Split('|', 2);
        if (parts.Length != 2 || !long.TryParse(parts[0], out var tokenId))
        {
            return AuthenticateResult.Fail("Invalid token.");
        }

        var tokenValue = parts[1];
        var hashed = SanctumTokenHasher.Sha256Hex(tokenValue);

        var pat = await _db.PersonalAccessTokens.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tokenId && x.Token == hashed);

        if (pat is null)
        {
            return AuthenticateResult.Fail("Invalid token.");
        }

        // Optional: enforce expiration if using expires_at
        if (pat.ExpiresAt is not null && pat.ExpiresAt.Value <= DateTime.UtcNow)
        {
            return AuthenticateResult.Fail("Token expired.");
        }

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == pat.TokenableId);
        if (user is null)
        {
            return AuthenticateResult.Fail("User not found.");
        }

        // Default to normal user when role is NULL
        var role = user.Role ?? 1;

        // Update last_used_at best-effort
        try
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE personal_access_tokens SET last_used_at = {DateTime.UtcNow} WHERE id = {tokenId}");
        }
        catch
        {
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("sub", user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("role", role.ToString())
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
