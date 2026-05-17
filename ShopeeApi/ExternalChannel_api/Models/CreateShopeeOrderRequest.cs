namespace ExternalChannel_api.Models;

public sealed class CreateShopeeOrderRequest
{
    public string? OrderSn { get; init; }
  public string Status { get; init; } = "NEW";
    public string BuyerName { get; init; } = string.Empty;
    public string BuyerPhone { get; init; } = string.Empty;
    public string ShippingAddress { get; init; } = string.Empty;
    public string Currency { get; init; } = "VND";
    public List<CreateShopeeOrderItemRequest> Items { get; init; } = new();
}

public sealed class CreateShopeeOrderItemRequest
{
    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Quantity { get; init; } = 1;
    public decimal UnitPrice { get; init; }
}
