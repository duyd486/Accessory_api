namespace ExternalChannel_api.Models;

public sealed record ShopeeOrder(
    string OrderSn,
    string BuyerName,
    string BuyerPhone,
    string ShippingAddress,
    decimal TotalAmount,
    string Currency,
    string Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ShopeeOrderItem> Items
);
