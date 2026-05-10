namespace Accessory_api.Contracts.Responses;

public sealed record OrderListRow(
    long id,
    int? order_code,
    long user_id,
    int status,
    DateTime? created_at,
    double? total_price,
    string? user_name,
    long product_id,
    string? product_name,
    int quantity,
    long? channel_id
);
