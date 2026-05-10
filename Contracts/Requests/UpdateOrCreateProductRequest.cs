using System.Text.Json.Serialization;

namespace Accessory_api.Contracts.Requests;

public class UpdateOrCreateProductRequest
{
    public int? Id { get; set; }

    public string? Name { get; set; }

    public long? CategoryId { get; set; }

    public string? Description { get; set; }

    public string? Brand { get; set; }

    public double? Price { get; set; }

    public int? Quantity { get; set; }

    public int? TotalSold { get; set; }

    public double? Score { get; set; }
}
