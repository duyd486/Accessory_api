using System.Text.Json;
using System.Text.Json.Serialization;
using ExternalChannel_api.Models;

namespace ExternalChannel_api.Services;

public sealed class ShopeeOrderFileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ShopeeOrderFileStore(IWebHostEnvironment env)
    {
        // Lưu file trong thư mục App_Data để dễ deploy / debug
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "shopee_orders.txt");
    }

    public async Task<IReadOnlyCollection<ShopeeOrder>> GetAllAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var orders = await ReadAllInternalAsync(cancellationToken);
            return orders
                .OrderByDescending(o => o.CreatedAt)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ShopeeOrder> CreateAsync(CreateShopeeOrderRequest request, CancellationToken cancellationToken)
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

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var orders = await ReadAllInternalAsync(cancellationToken);
            orders.Add(order);
            await WriteAllInternalAsync(orders, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        return order;
    }

    private async Task<List<ShopeeOrder>> ReadAllInternalAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new List<ShopeeOrder>();
        }

        var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<ShopeeOrder>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<ShopeeOrder>>(json, JsonOptions) ?? new List<ShopeeOrder>();
        }
        catch (JsonException)
        {
            // Nếu file bị hỏng format thì không crash API
            return new List<ShopeeOrder>();
        }
    }

    private async Task WriteAllInternalAsync(List<ShopeeOrder> orders, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(orders, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken);
    }
}
