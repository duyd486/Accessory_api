using System.Text.Json.Serialization;

namespace Accessory_api.Contracts.Requests;

public sealed class UpdateOrCreateCategoryRequest
{
    public long? Id { get; set; }

    public string? Title { get; set; }

    [JsonPropertyName("parent_id")]
    public long? ParentId { get; set; }
}
