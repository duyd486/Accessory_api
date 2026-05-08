namespace Accessory_api.Models;

public sealed class Category
{
    public long Id { get; set; }
    public string? Title { get; set; }
    public string? ThumbnailUrl { get; set; }
    public long ParentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
