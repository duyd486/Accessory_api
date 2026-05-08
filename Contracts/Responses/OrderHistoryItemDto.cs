namespace Accessory_api.Contracts.Responses;

public sealed record OrderHistoryItemDto(
    long BillId,
    int? BillCode,
    int BillStatus,
    double? BillTotalPrice,
    int DetailQuantity,
    double? DetailTotalPrice,
    long ProductId,
    string? ProductName,
    string? ProductThumbnail,
    double? ProductPrice,
    long CategoryId,
    string? CategoryTitle,
    string? CategoryThumbnail
);
