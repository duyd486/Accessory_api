namespace Accessory_api.Services;

public interface IPayOSService
{
    Task<PayOSCreateLinkResult?> CreatePaymentLinkAsync(int orderCode, double totalPrice, IReadOnlyList<PayOSItem> items);
    Task<PayOSPaymentStatusResult?> GetPaymentStatusAsync(int orderCode);
}

public sealed record PayOSItem(string Name, int Quantity, double Price);

public sealed record PayOSCreateLinkResult(string CheckoutUrl);

public sealed record PayOSPaymentStatusResult(string Status);
