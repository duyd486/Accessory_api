namespace Vibra_Dotnet_api.Models;

public sealed class BillDetail
{
    public long Id { get; set; }
    public long? BillId { get; set; }
    public long? ProductId { get; set; }
    public int? Quantity { get; set; }
    public double? TotalPrice { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
