using System.Text.Json.Serialization;

namespace Vibra_Dotnet_api.Contracts.Requests;

public sealed record UpdateProfileRequest(
    string? Name,
    string? Email,
    string? Phone,
    string? Address,
    [property: JsonPropertyName("current_password")] string? CurrentPassword,
    [property: JsonPropertyName("new_password")] string? NewPassword,
    [property: JsonPropertyName("new_password_confirmation")] string? NewPasswordConfirmation
);
