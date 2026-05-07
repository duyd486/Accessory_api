using System.Text.Json.Serialization;

namespace Vibra_Dotnet_api.Contracts.Requests;

public sealed record LoginRequest(string? Email, string? Password);

public sealed record SignupRequest(
    string? Name,
    string? Email,
    string? Password,
    [property: JsonPropertyName("password_confirmation")] string? PasswordConfirmation
);

public sealed record FirebaseSigninRequest(string? IdToken);
