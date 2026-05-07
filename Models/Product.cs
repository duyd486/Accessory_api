namespace Vibra_Dotnet_api.Models;

public sealed class Product
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public string? ThumbnailUrl { get; set; }
    public double Price { get; set; }
    public double Score { get; set; }
    public int TotalSold { get; set; }
    public long CategoryId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int Quantity { get; set; }
    public string? Description { get; set; }
    public string? Brand { get; set; }
}
