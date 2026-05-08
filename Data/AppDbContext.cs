using Microsoft.EntityFrameworkCore;
using Vibra_Dotnet_api.Models;

namespace Vibra_Dotnet_api.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillDetail> BillDetails => Set<BillDetail>();
    public DbSet<PersonalAccessToken> PersonalAccessTokens => Set<PersonalAccessToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name).HasColumnName("name");
            entity.Property(x => x.Email).HasColumnName("email");
            entity.Property(x => x.Password).HasColumnName("password");
            entity.Property(x => x.Role).HasColumnName("role");
            entity.Property(x => x.Avatar).HasColumnName("avatar");
            entity.Property(x => x.Phone).HasColumnName("phone");
            entity.Property(x => x.Address).HasColumnName("address");

            entity.HasIndex(x => x.Email).IsUnique(false);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("categories");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Title).HasColumnName("title");
            entity.Property(x => x.ThumbnailUrl).HasColumnName("thumbnail_url");
            entity.Property(x => x.ParentId).HasColumnName("parent_id");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name).HasColumnName("name");
            entity.Property(x => x.ThumbnailUrl).HasColumnName("thumbnail_url");
            entity.Property(x => x.Price).HasColumnName("price");
            entity.Property(x => x.Score).HasColumnName("score");
            entity.Property(x => x.TotalSold).HasColumnName("total_sold");
            entity.Property(x => x.CategoryId).HasColumnName("category_id");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            entity.Property(x => x.Quantity).HasColumnName("quantity");
            entity.Property(x => x.Description).HasColumnName("description");
            entity.Property(x => x.Brand).HasColumnName("brand");

            entity.HasIndex(x => x.CategoryId);
        });

        modelBuilder.Entity<Bill>(entity =>
        {
            entity.ToTable("bills");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.OrderCode).HasColumnName("order_code");
            entity.Property(x => x.TotalPrice).HasColumnName("total_price");
            entity.Property(x => x.Status).HasColumnName("status");
            entity.Property(x => x.PaymentMethod).HasColumnName("payment_method");
            entity.Property(x => x.Phone).HasColumnName("phone");
            entity.Property(x => x.Address).HasColumnName("address");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<BillDetail>(entity =>
        {
            entity.ToTable("bill_details");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.BillId).HasColumnName("bill_id");
            entity.Property(x => x.ProductId).HasColumnName("product_id");
            entity.Property(x => x.Quantity).HasColumnName("quantity");
            entity.Property(x => x.TotalPrice).HasColumnName("total_price");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(x => x.BillId);
            entity.HasIndex(x => x.ProductId);
        });

        modelBuilder.Entity<PersonalAccessToken>(entity =>
        {
            entity.ToTable("personal_access_tokens");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.TokenableType).HasColumnName("tokenable_type");
            entity.Property(x => x.TokenableId).HasColumnName("tokenable_id");
            entity.Property(x => x.Name).HasColumnName("name");
            entity.Property(x => x.Token).HasColumnName("token");
            entity.Property(x => x.Abilities).HasColumnName("abilities");
            entity.Property(x => x.LastUsedAt).HasColumnName("last_used_at");
            entity.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(x => x.Token);
            entity.HasIndex(x => new { x.TokenableType, x.TokenableId });
        });
    }
}
