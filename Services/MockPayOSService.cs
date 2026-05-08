namespace Accessory_api.Services;

public sealed class MockPayOSService : IPayOSService
{
    private readonly IConfiguration _configuration;

    public MockPayOSService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<PayOSCreateLinkResult?> CreatePaymentLinkAsync(int orderCode, double totalPrice, IReadOnlyList<PayOSItem> items)
    {
        var baseUrl = (_configuration["PayOS:MockCheckoutBaseUrl"] ?? "https://payos.local/checkout").TrimEnd('/');
        return Task.FromResult<PayOSCreateLinkResult?>(new PayOSCreateLinkResult($"{baseUrl}/{orderCode}"));
    }

    public Task<PayOSPaymentStatusResult?> GetPaymentStatusAsync(int orderCode)
    {
        // For testing: always return PROCESSING unless overridden.
        var status = _configuration["PayOS:MockStatus"] ?? "PROCESSING";
        return Task.FromResult<PayOSPaymentStatusResult?>(new PayOSPaymentStatusResult(status));
    }
}
