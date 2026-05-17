using PayOS;
using PayOS.Models;
using PayOS.Models.V2.PaymentRequests;

namespace Accessory_api.Services;

public sealed class MockPayOSService : IPayOSService
{
    private readonly IConfiguration _configuration;
    private readonly PayOSClient _client;

    public MockPayOSService(IConfiguration configuration)
    {
        _configuration = configuration;

        // Prefer appsettings.json but support environment variables like Laravel .env:
        // PAYOS_CLIENT_ID, PAYOS_API_KEY, PAYOS_CHECKSUM_KEY
        var clientId = _configuration["PayOS:ClientId"] ?? Environment.GetEnvironmentVariable("PAYOS_CLIENT_ID");
        var apiKey = _configuration["PayOS:ApiKey"] ?? Environment.GetEnvironmentVariable("PAYOS_API_KEY");
        var checksumKey = _configuration["PayOS:ChecksumKey"] ?? Environment.GetEnvironmentVariable("PAYOS_CHECKSUM_KEY");

        if (!string.IsNullOrWhiteSpace(clientId)
            && !string.IsNullOrWhiteSpace(apiKey)
            && !string.IsNullOrWhiteSpace(checksumKey))
        {
            _client = new PayOSClient(clientId, apiKey, checksumKey);
        }
        else
        {
            // SDK will also read env vars by default if present.
            _client = new PayOSClient();
        }
    }

    public Task<PayOSCreateLinkResult?> CreatePaymentLinkAsync(int orderCode, double totalPrice, IReadOnlyList<PayOSItem> items)
    {
        return CreatePaymentLinkCoreAsync(orderCode, totalPrice, items);
    }

    public Task<PayOSPaymentStatusResult?> GetPaymentStatusAsync(int orderCode)
    {
        return GetPaymentStatusCoreAsync(orderCode);
    }

    private async Task<PayOSCreateLinkResult?> CreatePaymentLinkCoreAsync(int orderCode, double totalPrice, IReadOnlyList<PayOSItem> items)
    {
        try
        {
            // PayOS expects VND integer amount
            var amount = Convert.ToInt64(Math.Round(totalPrice, MidpointRounding.AwayFromZero));

            var returnUrl = _configuration["PayOS:ReturnUrl"]
                ?? _configuration["App:ClientUrl"]
                ?? "http://localhost:5173";
            var cancelUrl = _configuration["PayOS:CancelUrl"]
                ?? _configuration["App:ClientUrl"]
                ?? "http://localhost:5173";

            var expiredMinutes = _configuration.GetValue<int?>("PayOS:ExpiredMinutes") ?? 10;
            var expiredAt = DateTimeOffset.UtcNow.AddMinutes(expiredMinutes).ToUnixTimeSeconds();

            var req = new CreatePaymentLinkRequest
            {
                OrderCode = orderCode,
                Amount = amount,
                Description = $"Thanh toan hoa don {orderCode}",
                ReturnUrl = returnUrl,
                CancelUrl = cancelUrl,
                ExpiredAt = expiredAt,
                Items = items.Select(i => new PaymentLinkItem
                {
                    Name = i.Name,
                    Quantity = i.Quantity,
                    Price = Convert.ToInt64(Math.Round(i.Price, MidpointRounding.AwayFromZero))
                }).ToList()
            };

            var res = await _client.PaymentRequests.CreateAsync(req);
            if (res is null || string.IsNullOrWhiteSpace(res.CheckoutUrl))
            {
                return null;
            }

            return new PayOSCreateLinkResult(res.CheckoutUrl);
        }
        catch
        {
            // Fallback to mock URL if PayOS is not configured / network issues.
            var baseUrl = (_configuration["PayOS:MockCheckoutBaseUrl"] ?? "https://payos.local/checkout").TrimEnd('/');
            return new PayOSCreateLinkResult($"{baseUrl}/{orderCode}");
        }
    }

    private async Task<PayOSPaymentStatusResult?> GetPaymentStatusCoreAsync(int orderCode)
    {
        try
        {
            var res = await _client.PaymentRequests.GetAsync(orderCode);
            if (res is null)
            {
                return null;
            }

            // SDK returns status string such as PAID / PENDING / PROCESSING / CANCELLED
            return new PayOSPaymentStatusResult(res.Status.ToString());
        }
        catch
        {
            // For testing: always return PROCESSING unless overridden.
            var status = _configuration["PayOS:MockStatus"] ?? "PROCESSING";
            return new PayOSPaymentStatusResult(status);
        }
    }
}
