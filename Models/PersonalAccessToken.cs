namespace Vibra_Dotnet_api.Models;

public sealed class PersonalAccessToken
{
    public long Id { get; set; }
    public string? TokenableType { get; set; }
    public long TokenableId { get; set; }
    public string? Name { get; set; }
    public string? Token { get; set; }
    public string? Abilities { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
