using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Accessory_api.Contracts;
using Accessory_api.Contracts.Requests;
using Accessory_api.Contracts.Responses;
using Accessory_api.Data;
using Accessory_api.Models;

namespace Accessory_api.Controllers;

[ApiController]
[Route("api")]
public sealed class ManageProductController : ControllerBase
{
    private const int DefaultLimit = 10;

    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public ManageProductController(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpGet("list-product")]
    public async Task<ActionResult<ApiResponse<object>>> ListProduct(
        [FromQuery(Name = "category_id")] long? categoryId,
        [FromQuery(Name = "search_key")] string? searchKey,
        [FromQuery(Name = "sort_type")] string? sortType,
        [FromQuery] int? offset)
    {
        try
        {
            var safeOffset = Math.Max(0, offset ?? 0);

            IQueryable<Product> query = _db.Products.AsNoTracking().Where(x => x.DeletedAt == null);

            if (categoryId is not null)
            {
                var category = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == categoryId.Value && x.DeletedAt == null);
                if (category is not null)
                {
                    if (category.ParentId == 0)
                    {
                        var childIds = await _db.Categories.AsNoTracking()
                            .Where(x => x.ParentId == categoryId.Value && x.DeletedAt == null)
                            .Select(x => x.Id)
                            .ToListAsync();

                        childIds.Add(categoryId.Value);
                        query = query.Where(x => childIds.Contains(x.CategoryId));
                    }
                    else
                    {
                        query = query.Where(x => x.CategoryId == categoryId.Value);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(searchKey))
            {
                query = query.Where(x => x.Name != null && EF.Functions.Like(x.Name, $"%{searchKey}%"));
            }

            query = (sortType ?? "default") switch
            {
                "newest" => query.OrderByDescending(x => x.CreatedAt),
                "featured" => query.OrderByDescending(x => x.TotalSold),
                "price_asc" => query.OrderBy(x => x.Price),
                "price_desc" => query.OrderByDescending(x => x.Price),
                _ => query.OrderBy(x => x.Id)
            };

            var list_products = await query
                .Skip(safeOffset)
                .Take(DefaultLimit)
                .Select(x => new
                {
                    id = x.Id,
                    name = x.Name,
                    thumbnail_url = x.ThumbnailUrl,
                    price = x.Price,
                    score = x.Score,
                    total_sold = x.TotalSold,
                    category_id = x.CategoryId,
                    created_at = x.CreatedAt,
                    quantity = x.Quantity,
                    description = x.Description
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Ok(new { list_products }));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail(ex.Message));
        }
    }

    [AllowAnonymous]
    [HttpGet("best-products")]
    public async Task<ActionResult<ApiResponse<object>>> BestProducts()
    {
        try
        {
            var best_products = await _db.Products.AsNoTracking()
                .Where(x => x.DeletedAt == null)
                .OrderByDescending(x => x.TotalSold)
                .Take(4)
                .Select(x => new
                {
                    id = x.Id,
                    name = x.Name,
                    thumbnail_url = x.ThumbnailUrl,
                    price = x.Price,
                    score = x.Score,
                    total_sold = x.TotalSold,
                    description = x.Description
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Ok(new { best_products }));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail(ex.Message));
        }
    }

    [AllowAnonymous]
    [HttpGet("product-detail")]
    public async Task<ActionResult<ApiResponse<object>>> ProductDetail([FromQuery(Name = "product_id")] long? productId)
    {
        if (productId is null)
        {
            return BadRequest(ApiResponse<object>.Fail("Validation error."));
        }

        try
        {
            var product = await (from p in _db.Products.AsNoTracking()
                                 join c in _db.Categories.AsNoTracking() on p.CategoryId equals c.Id into pc
                                 from c in pc.DefaultIfEmpty()
                                 where p.Id == productId.Value && p.DeletedAt == null
                                 select new
                                 {
                                     id = p.Id,
                                     name = p.Name,
                                     thumbnail_url = p.ThumbnailUrl,
                                     price = p.Price,
                                     description = p.Description,
                                     score = p.Score,
                                     category_id = p.CategoryId,
                                     total_sold = p.TotalSold,
                                     quantity = p.Quantity,
                                     category_title = c != null ? c.Title : null
                                 })
                .FirstOrDefaultAsync();

            if (product is null)
            {
                return NotFound(ApiResponse<object>.Fail("Not found"));
            }

            var similar_products = await _db.Products.AsNoTracking()
                .Where(x => x.DeletedAt == null && x.CategoryId == product.category_id && x.Id != product.id)
                .OrderByDescending(x => x.TotalSold)
                .Take(5)
                .Select(x => new
                {
                    id = x.Id,
                    name = x.Name,
                    thumbnail_url = x.ThumbnailUrl,
                    price = x.Price,
                    score = x.Score
                })
                .ToListAsync();

            FormattableString feedbackSql = $@"
SELECT DISTINCT
    feedbacks.id AS id,
    feedbacks.comment AS comment,
    feedbacks.score AS score,
    feedbacks.created_at AS created_at,
    users.name AS user_name,
    users.avatar AS user_avatar
FROM feedbacks
INNER JOIN users ON users.id = feedbacks.user_id
INNER JOIN bills ON bills.id = feedbacks.bill_id
INNER JOIN bill_details ON bill_details.bill_id = bills.id
WHERE bill_details.product_id = {productId.Value}
  AND feedbacks.type = 0
  AND feedbacks.status = 1
ORDER BY feedbacks.created_at DESC";

            var product_feedbacks = await _db.Database.SqlQuery<ProductFeedbackRow>(feedbackSql).ToListAsync();
            var feedback_count = product_feedbacks.Count;

            return Ok(ApiResponse<object>.Ok(new
            {
                product,
                similar_products,
                product_feedbacks,
                feedback_count
            }));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail(ex.Message));
        }
    }

    [AllowAnonymous]
    [HttpGet("similar-products")]
    public async Task<ActionResult<ApiResponse<object>>> SimilarProduct([FromQuery(Name = "product_id")] long? productId)
    {
        if (productId is null)
        {
            return BadRequest(ApiResponse<object>.Fail("Validation error."));
        }

        try
        {
            var product = await _db.Products.AsNoTracking()
                .Where(x => x.Id == productId.Value && x.DeletedAt == null)
                .Select(x => new { x.Id, x.CategoryId })
                .FirstOrDefaultAsync();

            if (product is null)
            {
                return Ok(ApiResponse<object>.Ok(new { similar_products = Array.Empty<object>() }));
            }

            var similar_products = await _db.Products.AsNoTracking()
                .Where(x => x.DeletedAt == null && x.CategoryId == product.CategoryId && x.Id != product.Id)
                .OrderByDescending(x => x.TotalSold)
                .Take(5)
                .Select(x => new
                {
                    id = x.Id,
                    name = x.Name,
                    thumbnail_url = x.ThumbnailUrl,
                    price = x.Price,
                    score = x.Score
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Ok(new { similar_products }));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpPost("update-or-create-product")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateOrCreateProduct(
        [FromForm] UpdateOrCreateProductRequest request,
        IFormFile? thumbnail)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.CategoryId is null)
        {
            return BadRequest(ApiResponse<object>.Fail("Validation error."));
        }

        var now = DateTime.UtcNow;

        try
        {
            Product? product = null;

            // UPDATE
            if (request.Id != null)
            {
                product = await _db.Products.FirstOrDefaultAsync(x =>
                    x.DeletedAt == null &&
                    x.Id == request.Id.Value);

                if (product == null)
                {
                    return NotFound(ApiResponse<object>.Fail("Không tìm thấy sản phẩm."));
                }
            }
            // CREATE
            else
            {
                product = new Product
                {
                    CreatedAt = now
                };

                _db.Products.Add(product);
            }

            product.Name = request.Name;
            product.CategoryId = request.CategoryId.Value;
            product.Description = request.Description;
            product.Brand = request.Brand;
            product.Price = request.Price ?? 0;
            product.Quantity = request.Quantity ?? 0;
            product.TotalSold = request.TotalSold ?? 0;
            product.Score = request.Score ?? 0;
            product.UpdatedAt = now;

            // Save trước để có Id
            await _db.SaveChangesAsync();

            // Upload ảnh
            if (thumbnail != null && thumbnail.Length > 0)
            {
                var uploadsFolder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "uploads",
                    "products");

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var fileName = $"{product.Id}.png";
                var filePath = Path.Combine(uploadsFolder, fileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await thumbnail.CopyToAsync(stream);
                }

                var baseUrl = (_configuration["App:BaseUrl"] ?? string.Empty).TrimEnd('/');

                product.ThumbnailUrl = string.IsNullOrEmpty(baseUrl)
                    ? $"/uploads/products/{fileName}"
                    : $"{baseUrl}/uploads/products/{fileName}";

                await _db.SaveChangesAsync();
            }

            var result = new
            {
                id = product.Id,
                name = product.Name,
                thumbnail_url = product.ThumbnailUrl,
                price = product.Price,
                score = product.Score,
                total_sold = product.TotalSold,
                category_id = product.CategoryId,
                created_at = product.CreatedAt,
                updated_at = product.UpdatedAt,
                quantity = product.Quantity,
                description = product.Description,
                brand = product.Brand
            };

            return Ok(ApiResponse<object>.Ok(
                result,
                request.Id != null
                    ? "Cập nhật sản phẩm thành công!"
                    : "Tạo sản phẩm thành công!"));
        }
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpGet("delete-product")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteProduct([FromQuery(Name = "product_id")] long? productId)
    {
        //if (!IsAdmin())
        //{
        //    return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("Chỉ admin mới có quyền xoá!"));
        //}

        if (productId is null)
        {
            return BadRequest(ApiResponse<object>.Fail("Validation error."));
        }

        var now = DateTime.UtcNow;
        try
        {
            var product = await _db.Products.FirstOrDefaultAsync(x => x.Id == productId.Value);
            if (product is not null)
            {
                product.DeletedAt = now;
                await _db.SaveChangesAsync();
            }

            return Ok(ApiResponse<object>.Ok(null, "Xóa sản phẩm thành công!"));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail(ex.Message));
        }
    }

    [AllowAnonymous]
    [HttpGet("list-product-v2")]
    public async Task<ActionResult<ApiResponse<object>>> ListProductV2(
        [FromQuery(Name = "category_id")] long? categoryId,
        [FromQuery(Name = "search_key")] string? searchKey,
        [FromQuery(Name = "sort_type")] string? sortType,
        [FromQuery] int? offset)
    {
        try
        {
            var safeOffset = Math.Max(0, offset ?? 0);

            IQueryable<Product> query = _db.Products.AsNoTracking().Where(x => x.DeletedAt == null);

            if (categoryId is not null)
            {
                var category = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == categoryId.Value && x.DeletedAt == null);
                if (category is not null)
                {
                    if (category.ParentId == 0)
                    {
                        var childIds = await _db.Categories.AsNoTracking()
                            .Where(x => x.ParentId == categoryId.Value && x.DeletedAt == null)
                            .Select(x => x.Id)
                            .ToListAsync();

                        childIds.Add(categoryId.Value);
                        query = query.Where(x => childIds.Contains(x.CategoryId));
                    }
                    else
                    {
                        query = query.Where(x => x.CategoryId == categoryId.Value);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(searchKey))
            {
                query = query.Where(x => x.Name != null && EF.Functions.Like(x.Name, $"%{searchKey}%"));
            }

            query = (sortType ?? "default") switch
            {
                "newest" => query.OrderByDescending(x => x.CreatedAt),
                "featured" => query.OrderByDescending(x => x.TotalSold),
                "price_asc" => query.OrderBy(x => x.Price),
                "price_desc" => query.OrderByDescending(x => x.Price),
                _ => query.OrderBy(x => x.Id)
            };

            var list_products = await query
                .Skip(safeOffset)
                .Take(500)
                .Select(x => new
                {
                    id = x.Id,
                    name = x.Name,
                    thumbnail_url = x.ThumbnailUrl,
                    price = x.Price,
                    score = x.Score,
                    total_sold = x.TotalSold,
                    category_id = x.CategoryId,
                    created_at = x.CreatedAt,
                    quantity = x.Quantity,
                    description = x.Description
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Ok(new { list_products }));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail(ex.Message));
        }
    }

    private bool IsAdmin()
    {
        var roleRaw = User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role);
        return int.TryParse(roleRaw, out var role) && role == 0;
    }
}
