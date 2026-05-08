namespace Accessory_api.Models;

public sealed class User
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public int? Role { get; set; }
    public string? Avatar { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
}
