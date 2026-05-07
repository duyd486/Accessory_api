using System.Text.Json.Serialization;

namespace Vibra_Dotnet_api.Contracts.Requests;

public sealed record SendFeedbackRequest(
    [property: JsonPropertyName("bill_id")] long? BillId,
    int? Score,
    string? Comment
);
