namespace ExternalChannel_api.Models;

public sealed record UpsertShopeeOrderRequest(
    string OrderSn,
    string? BuyerName,
    string? BuyerPhone,
    string? ShippingAddress,
    double TotalAmount,
    string? Currency,
    string? Status,
    DateTimeOffset? CreatedAt,
    List<UpsertShopeeOrderItemRequest>? Items
);

public sealed record UpsertShopeeOrderItemRequest(
    string Sku,
    string? Name,
    int Quantity,
    double UnitPrice
);
