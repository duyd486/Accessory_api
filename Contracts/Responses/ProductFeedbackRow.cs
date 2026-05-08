namespace Accessory_api.Contracts.Responses;

public sealed record ProductFeedbackRow(
    long id,
    string? comment,
    int? score,
    DateTime? created_at,
    string? user_name,
    string? user_avatar
);
