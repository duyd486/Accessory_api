using System.Text.Json.Serialization;

namespace Vibra_Dotnet_api.Contracts.Requests;

public sealed record UpdateOrderStatusRequest(
    [property: JsonPropertyName("order_id")] long? OrderId,
    int? Status
);
