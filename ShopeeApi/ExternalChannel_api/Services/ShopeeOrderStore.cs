using System.Collections.Concurrent;
using ExternalChannel_api.Models;

namespace ExternalChannel_api.Services;

public sealed class ShopeeOrderStore
{
    private readonly ConcurrentDictionary<string, ShopeeOrder> _orders = new(StringComparer.OrdinalIgnoreCase);

    public ShopeeOrderStore()
    {
        // seed demo data
        var seed = new ShopeeOrder(
            OrderSn: "240101-DEMO-0001",
            BuyerName: "Demo Buyer",
            BuyerPhone: "0900000000",
            ShippingAddress: "HCM, VN",
            TotalAmount: 150_000m,
            Currency: "VND",
            Status: "NEW",
            CreatedAt: DateTimeOffset.UtcNow,
            Items: new List<ShopeeOrderItem>
            {
                new("SKU-001", "Demo Item", 1, 150_000m)
            }
        );

        _orders[seed.OrderSn] = seed;
    }

    public IReadOnlyCollection<ShopeeOrder> GetAll()
    {
        return _orders.Values
            .OrderByDescending(o => o.CreatedAt)
            .ToArray();
    }

    public ShopeeOrder Create(CreateShopeeOrderRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            throw new ArgumentException("Items is required", nameof(request));
        }

        var orderSn = string.IsNullOrWhiteSpace(request.OrderSn)
            ? $"{DateTimeOffset.UtcNow:yyMMdd}-SIM-{Guid.NewGuid():N}"[..20]
            : request.OrderSn.Trim();

        var items = request.Items
            .Where(i => i is not null)
            .Select(i => new ShopeeOrderItem(
                Sku: i.Sku,
                Name: i.Name,
                Quantity: i.Quantity,
                UnitPrice: i.UnitPrice
            ))
            .ToList();

        var total = items.Sum(i => i.UnitPrice * i.Quantity);

        var order = new ShopeeOrder(
            OrderSn: orderSn,
            BuyerName: request.BuyerName,
            BuyerPhone: request.BuyerPhone,
            ShippingAddress: request.ShippingAddress,
            TotalAmount: total,
            Currency: string.IsNullOrWhiteSpace(request.Currency) ? "VND" : request.Currency,
            Status: "NEW",
            CreatedAt: DateTimeOffset.UtcNow,
            Items: items
        );

        _orders[order.OrderSn] = order;
        return order;
    }
}
