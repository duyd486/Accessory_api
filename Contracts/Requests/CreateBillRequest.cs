using System.Text.Json.Serialization;

namespace Accessory_api.Contracts.Requests;

public sealed record CreateBillItemRequest(
    long Id,
    int Quantity,
    [property: JsonPropertyName("total_price")] double TotalPrice,
    double Price
);

public sealed record CreateBillRequest(
    [property: JsonPropertyName("channelId")] long? ChannelId,
    [property: JsonPropertyName("channel_id")] long? ChannelIdSnake,
    [property: JsonPropertyName("payment_method")] string? PaymentMethod,
    [property: JsonPropertyName("total_price")] double? TotalPrice,
    string? Phone,
    string? Address,
    List<CreateBillItemRequest>? Items
);
