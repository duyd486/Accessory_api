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
public sealed class ManageUserController : ControllerBase
{
    private readonly AppDbContext _db;

    public ManageUserController(AppDbContext db)
    {
        _db = db;
    }

    [Authorize]
    [HttpPost("create-staff")]
    public async Task<ActionResult<ApiResponse<object>>> CreateStaff([FromBody] CreateStaffRequest request)
    {
        if (!IsAdmin())
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("Ch? admin m?i có quy?n t?o nhân viên!"));
        }

        if (string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(request.PasswordConfirmation))
        {
            return BadRequest(ApiResponse<object>.Fail("Validation error."));
        }

        if (!IsValidEmail(request.Email))
        {
            return BadRequest(ApiResponse<object>.Fail("Validation error."));
        }

        if (request.Password.Length < 8)
        {
            return BadRequest(ApiResponse<object>.Fail("Validation error."));
        }

        if (request.Password != request.PasswordConfirmation)
        {
            return BadRequest(ApiResponse<object>.Fail("Validation error."));
        }

        try
        {
            var exists = await _db.Users.AnyAsync(x => x.Email == request.Email);
            if (exists)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("This email is already associated with an account."));
            }

            var staff = new User
            {
                Name = request.Name,
                Email = request.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = 2,
                Phone = request.Phone,
                Address = request.Address
            };

            _db.Users.Add(staff);
            await _db.SaveChangesAsync();

            var dto = new
            {
                staff.Id,
                staff.Name,
                staff.Email,
                role = staff.Role,
                staff.Phone,
                staff.Address
            };

            return Ok(ApiResponse<object>.Ok(new { user = dto }, "T?o nhân viên thành công."));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpGet("user-infor")]
    public async Task<ActionResult<ApiResponse<object>>> UserInfor()
    {
        try
        {
            var userId = GetUserId();
            if (userId is null)
            {
                return Unauthorized(ApiResponse<object>.Fail("B?n ch?a ??ng nh?p."));
            }

            var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId.Value);
            if (user is null)
            {
                return NotFound(ApiResponse<object>.Fail("Not found"));
            }

            var dto = new
            {
                user.Id,
                user.Name,
                user.Email,
                role = user.Role,
                user.Avatar,
                user.Phone,
                user.Address
            };

            return Ok(ApiResponse<object>.Ok(dto));
        }
        catch
        {
            return NotFound(ApiResponse<object>.Fail("Not found"));
        }
    }

    [Authorize]
    [HttpGet("order-history")]
    public async Task<ActionResult<ApiResponse<object>>> OrderHistory()
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse<object>.Fail("B?n ch?a ??ng nh?p."));
        }

        try
        {
            var orderPreparing = await QueryOrdersByStatusAsync(userId.Value, new[] { 3, 4 });
            var orderShipping = await QueryOrdersByStatusAsync(userId.Value, new[] { 5 });
            var orderCompleted = await QueryOrdersByStatusAsync(userId.Value, new[] { 6 });

            return Ok(ApiResponse<object>.Ok(new
            {
                orderPreparing,
                orderShipping,
                orderCompleted
            }));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpPost("update-profile")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId is null)
            {
                return Unauthorized(ApiResponse<object>.Fail("B?n ch?a ??ng nh?p."));
            }

            var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId.Value);
            if (user is null)
            {
                return Unauthorized(ApiResponse<object>.Fail("B?n ch?a ??ng nh?p."));
            }

            var errors = new Dictionary<string, string[]>();

            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 255)
            {
                errors["name"] = new[] { "The name field is required." };
            }

            if (string.IsNullOrWhiteSpace(request.Email) || !IsValidEmail(request.Email))
            {
                errors["email"] = new[] { "The email field is required." };
            }
            else
            {
                var emailExists = await _db.Users.AnyAsync(x => x.Email == request.Email && x.Id != user.Id);
                if (emailExists)
                {
                    errors["email"] = new[] { "The email has already been taken." };
                }
            }

            if (!string.IsNullOrWhiteSpace(request.Phone) && request.Phone.Length > 20)
            {
                errors["phone"] = new[] { "The phone field is too long." };
            }

            if (!string.IsNullOrWhiteSpace(request.Address) && request.Address.Length > 255)
            {
                errors["address"] = new[] { "The address field is too long." };
            }

            if (errors.Count > 0)
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity,
                    new ApiResponse<object> { Status = false, Data = errors, Message = "D? li?u không h?p l?." });
            }

            user.Name = request.Name;
            user.Email = request.Email;
            user.Phone = request.Phone;
            user.Address = request.Address;

            var changingPassword = !string.IsNullOrWhiteSpace(request.CurrentPassword)
                                  || !string.IsNullOrWhiteSpace(request.NewPassword)
                                  || !string.IsNullOrWhiteSpace(request.NewPasswordConfirmation);

            if (changingPassword)
            {
                if (string.IsNullOrWhiteSpace(request.CurrentPassword)
                    || string.IsNullOrWhiteSpace(request.NewPassword)
                    || string.IsNullOrWhiteSpace(request.NewPasswordConfirmation))
                {
                    return StatusCode(StatusCodes.Status422UnprocessableEntity,
                        new ApiResponse<object> { Status = false, Data = null, Message = "Vui lòng nh?p ??y ?? m?t kh?u." });
                }

                if (string.IsNullOrEmpty(user.Password) || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.Password))
                {
                    return StatusCode(StatusCodes.Status422UnprocessableEntity,
                        new ApiResponse<object> { Status = false, Data = null, Message = "M?t kh?u hi?n t?i không ?úng." });
                }

                if (request.NewPassword != request.NewPasswordConfirmation)
                {
                    return StatusCode(StatusCodes.Status422UnprocessableEntity,
                        new ApiResponse<object> { Status = false, Data = null, Message = "Xác nh?n m?t kh?u m?i không kh?p." });
                }

                if (request.NewPassword.Length < 8)
                {
                    return StatusCode(StatusCodes.Status422UnprocessableEntity,
                        new ApiResponse<object> { Status = false, Data = null, Message = "M?t kh?u m?i ph?i ít nh?t 8 ký t?." });
                }

                user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            }

            await _db.SaveChangesAsync();

            var userDto = new
            {
                user.Id,
                user.Name,
                user.Email,
                role = user.Role,
                user.Avatar,
                user.Phone,
                user.Address
            };

            return Ok(ApiResponse<object>.Ok(new { user = userDto }, "C?p nh?t thông tin thành công."));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail(ex.Message));
        }
    }

    private long? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(raw, out var id) ? id : null;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var _ = new System.Net.Mail.MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool IsAdmin()
    {
        var roleRaw = User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role);
        return int.TryParse(roleRaw, out var role) && role == 0;
    }

    private async Task<List<OrderHistoryItemDto>> QueryOrdersByStatusAsync(long userId, int[] statuses)
    {
        // Equivalent of the Laravel query builder joins in `orderHistory`.
        // NOTE: Uses IN clause built from fixed, server-side statuses.
        var inClause = string.Join(",", statuses.Select(s => s.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        FormattableString sql = $@"
SELECT
    bills.id AS BillId,
    bills.order_code AS BillCode,
    bills.status AS BillStatus,
    bills.total_price AS BillTotalPrice,
    bill_details.quantity AS DetailQuantity,
    bill_details.total_price AS DetailTotalPrice,
    products.id AS ProductId,
    products.name AS ProductName,
    products.thumbnail_url AS ProductThumbnail,
    products.price AS ProductPrice,
    categories.id AS CategoryId,
    categories.title AS CategoryTitle,
    categories.thumbnail_url AS CategoryThumbnail
FROM bills
INNER JOIN bill_details ON bill_details.bill_id = bills.id
INNER JOIN products ON products.id = bill_details.product_id
INNER JOIN categories ON categories.id = products.category_id
WHERE bills.user_id = {userId} AND bills.status IN ({inClause})
ORDER BY bills.id DESC";

        return await _db.Database.SqlQuery<OrderHistoryItemDto>(sql).ToListAsync();
    }
}
