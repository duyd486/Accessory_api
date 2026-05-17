using System.Text.Json.Serialization;

namespace Accessory_api.Contracts.Requests;

public sealed record CreateStaffRequest(
    string? Name,
    string? Email,
    string? Password,
    [property: JsonPropertyName("password_confirmation")] string? PasswordConfirmation,
    string? Phone,
    string? Address
);
