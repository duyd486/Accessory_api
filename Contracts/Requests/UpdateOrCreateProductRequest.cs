using System.Text.Json.Serialization;

namespace Accessory_api.Contracts.Requests;

public sealed record UpdateOrCreateProductRequest(
    string? Name,
    [property: JsonPropertyName("category_id")] long? CategoryId,
    string? Description,
    string? Brand,
    double? Price,
    int? Quantity,
    [property: JsonPropertyName("total_sold")] int? TotalSold,
    double? Score
);
