using System.Text.Json.Serialization;

namespace Accessory_api.Contracts.Requests;

public sealed record UpdateOrCreateCategoryRequest(
    string? Title,
    [property: JsonPropertyName("parent_id")] long? ParentId
);
