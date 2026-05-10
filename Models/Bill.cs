namespace Accessory_api.Models;

public sealed class Bill
{
    public const int PAYMENT_METHOD_ONLINE = 0;
    public const int PAYMENT_METHOD_OFFLINE = 1;

    public const int STATUS_CANCELLED = 0;
    public const int STATUS_PROCESSING = 1;
    public const int STATUS_PENDING = 2;
    public const int STATUS_PAID = 3;
    public const int STATUS_PREPARING = 4;
    public const int STATUS_SHIPPING = 5;
    public const int STATUS_DONE = 6;

    public long Id { get; set; }
    public long? UserId { get; set; }
    public int? OrderCode { get; set; }
    public double? TotalPrice { get; set; }
    public int? Status { get; set; }
    public int? PaymentMethod { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? ChannelId { get; set; }
}
