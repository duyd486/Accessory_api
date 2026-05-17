namespace ExternalChannel_api.Models;

public sealed record ShopeeOrderItem(
    string Sku,
    string Name,
    int Quantity,
    decimal UnitPrice
);
