using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Accessory_api.Contracts;
using Accessory_api.Contracts.Requests;
using Accessory_api.Data;
using Accessory_api.Models;

namespace Accessory_api.Controllers;

[ApiController]
[Route("api")]
public sealed class ManageCategoryController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public ManageCategoryController(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpGet("list-category")]
    public async Task<ActionResult<ApiResponse<object>>> ListCategory()
    {
        try
        {
            // Laravel returns root categories (parent_id = 0) with 2 levels of children.
            var all = await _db.Categories.AsNoTracking()
                .Where(x => x.DeletedAt == null)
                .Select(x => new CategoryNode(x.Id, x.Title, x.ParentId, x.ThumbnailUrl))
                .ToListAsync();

            var childrenByParent = all
                .GroupBy(x => x.ParentId)
                .ToDictionary(g => g.Key, g => g.ToList());

            object MapNode(CategoryNode node, int depth)
            {
                if (depth <= 0)
                {
                    return new
                    {
                        id = node.Id,
                        title = node.Title,
                        parent_id = node.ParentId,
                        thumbnail_url = node.ThumbnailUrl
                    };
                }

                childrenByParent.TryGetValue(node.Id, out var kids);
                var mappedKids = (kids ?? new List<CategoryNode>()).Select(k => MapNode(k, depth - 1)).ToList();

                return new
                {
                    id = node.Id,
                    title = node.Title,
                    parent_id = node.ParentId,
                    thumbnail_url = node.ThumbnailUrl,
                    children = mappedKids
                };
            }

            var roots = all.Where(x => x.ParentId == 0)
                .Select(x => MapNode(x, depth: 2))
                .ToList();

            return Ok(ApiResponse<object>.Ok(new { categories = roots }));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail(ex.Message));
        }
    }

    private sealed record CategoryNode(long Id, string? Title, long ParentId, string? ThumbnailUrl);

    [Authorize]
    [HttpPost("update-or-create-cate")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateOrCreateCate(
        [FromForm] UpdateOrCreateCategoryRequest request,
        IFormFile? thumbnail)
    {
        if (!IsAdmin())
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("Chỉ admin mới có quyền chỉnh sửa dữ liệu!"));
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.ParentId is null)
        {
            return BadRequest(ApiResponse<object>.Fail("Validation error."));
        }

        var now = DateTime.UtcNow;
        try
        {
            Category? category = null;

            // UPDATE
            if (request.Id is not null)
            {
                category = await _db.Categories.FirstOrDefaultAsync(x =>
                    x.DeletedAt == null &&
                    x.Id == request.Id.Value);

                if (category is null)
                {
                    return NotFound(ApiResponse<object>.Fail("Không tìm thấy danh mục."));
                }
            }
            // CREATE
            else
            {
                category = new Category
                {
                    CreatedAt = now
                };

                _db.Categories.Add(category);
            }

            category.Title = request.Title;
            category.ParentId = request.ParentId.Value;
            category.UpdatedAt = now;

            // Save trước để có Id khi tạo mới
            await _db.SaveChangesAsync();

            // Upload ảnh (nếu có)
            if (thumbnail != null && thumbnail.Length > 0)
            {
                var uploadsFolder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "uploads",
                    "categories");

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var ext = Path.GetExtension(thumbnail.FileName);
                if (string.IsNullOrWhiteSpace(ext))
                {
                    ext = ".png";
                }

                var fileName = $"{category.Id}{ext}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await thumbnail.CopyToAsync(stream);
                }

                var baseUrl = (_configuration["App:BaseUrl"] ?? string.Empty).TrimEnd('/');
                category.ThumbnailUrl = string.IsNullOrEmpty(baseUrl)
                    ? $"/uploads/categories/{fileName}"
                    : $"{baseUrl}/uploads/categories/{fileName}";

                await _db.SaveChangesAsync();
            }

            var dto = new
            {
                id = category.Id,
                title = category.Title,
                parent_id = category.ParentId,
                thumbnail_url = category.ThumbnailUrl
            };

            return Ok(ApiResponse<object>.Ok(
                dto,
                request.Id is not null
                    ? "Cập nhật danh mục thành công!"
                    : "Tạo danh mục thành công!"));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpGet("delete-cate")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCate([FromQuery(Name = "category_id")] long? categoryId)
    {
        if (!IsAdmin())
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("Chỉ admin mới có quyền xoá!"));
        }

        if (categoryId is null)
        {
            return BadRequest(ApiResponse<object>.Fail("Validation error."));
        }

        var now = DateTime.UtcNow;
        try
        {
            var category = await _db.Categories.FirstOrDefaultAsync(x => x.Id == categoryId.Value);
            if (category is not null)
            {
                category.DeletedAt = now;
                await _db.SaveChangesAsync();
            }

            return Ok(ApiResponse<object>.Ok(null));
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
