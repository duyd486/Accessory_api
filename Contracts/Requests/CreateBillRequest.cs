using System.Text.Json.Serialization;

namespace Accessory_api.Contracts.Requests;

public sealed record CreateBillItemRequest(
    long Id,
    int Quantity,
    [property: JsonPropertyName("total_price")] double TotalPrice,
    double Price
);

public sealed record CreateBillRequest(
    long? ChannelId,
    [property: JsonPropertyName("payment_method")] string? PaymentMethod,
    [property: JsonPropertyName("total_price")] double? TotalPrice,
    string? Phone,
    string? Address,
    List<CreateBillItemRequest>? Items
);
