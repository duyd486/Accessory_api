using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Vibra_Dotnet_api.Contracts;
using Vibra_Dotnet_api.Contracts.Requests;
using Vibra_Dotnet_api.Data;
using Vibra_Dotnet_api.Models;

namespace Vibra_Dotnet_api.Controllers;

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
    public async Task<ActionResult<ApiResponse<object>>> UpdateOrCreateCate([FromBody] UpdateOrCreateCategoryRequest request)
    {
        if (!IsAdmin())
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("Ch? admin m?i có quy?n ch?nh s?a d? li?u!"));
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.ParentId is null)
        {
            return BadRequest(ApiResponse<object>.Fail("Validation error."));
        }

        var now = DateTime.UtcNow;
        try
        {
            var category = await _db.Categories
                .FirstOrDefaultAsync(x => x.DeletedAt == null && x.Title == request.Title && x.ParentId == request.ParentId.Value);

            if (category is null)
            {
                category = new Category
                {
                    Title = request.Title,
                    ParentId = request.ParentId.Value,
                    CreatedAt = now
                };
                _db.Categories.Add(category);
            }

            var baseUrl = (_configuration["App:BaseUrl"] ?? string.Empty).TrimEnd('/');
            category.ThumbnailUrl = string.IsNullOrEmpty(baseUrl)
                ? $"/uploads/products/thumbnail_urls/{request.Title}.jpg"
                : $"{baseUrl}/uploads/products/thumbnail_urls/{request.Title}.jpg";
            category.UpdatedAt = now;

            await _db.SaveChangesAsync();

            var dto = new
            {
                id = category.Id,
                title = category.Title,
                parent_id = category.ParentId,
                thumbnail_url = category.ThumbnailUrl
            };

            return Ok(ApiResponse<object>.Ok(dto));
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
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("Ch? admin m?i có quy?n xoá!"));
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
        var roleRaw = User.FindFirstValue("role");
        return int.TryParse(roleRaw, out var role) && role == 1;
    }
}
