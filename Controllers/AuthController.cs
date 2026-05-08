using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Accessory_api.Contracts;
using Accessory_api.Contracts.Requests;
using Accessory_api.Data;
using Accessory_api.Services;

namespace Accessory_api.Controllers;

[ApiController]
[Route("api")]
public sealed class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;

    public AuthController(AppDbContext db, ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<object>>> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(ApiResponse<object>.Fail("Validation error."));
            }

            var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == request.Email);

            if (user is null || string.IsNullOrEmpty(user.Password) || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            {
                return NotFound(ApiResponse<object>.Fail("User not found. Please check your information."));
            }

            // Prefer returning a Sanctum-compatible token if the table exists and client expects `id|token`.
            // Otherwise fallback to JWT.
            var token = _tokenService.CreateToken(user);

            var userDto = new
            {
                user.Id,
                user.Name,
                user.Email,
                role = user.Role
            };

            return Ok(ApiResponse<object>.Ok(new { user = userDto, token }));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail(ex.Message));
        }
    }

    [AllowAnonymous]
    [HttpPost("signup")]
    public async Task<ActionResult<ApiResponse<object>>> Signup([FromBody] SignupRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 255)
            {
                return BadRequest(ApiResponse<object>.Fail("Validation error."));
            }

            if (string.IsNullOrWhiteSpace(request.Email) || !IsValidEmail(request.Email))
            {
                return BadRequest(ApiResponse<object>.Fail("Validation error."));
            }

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            {
                return BadRequest(ApiResponse<object>.Fail("Validation error."));
            }

            if (request.PasswordConfirmation is null || request.PasswordConfirmation != request.Password)
            {
                return BadRequest(ApiResponse<object>.Fail("Validation error."));
            }

            var exists = await _db.Users.AnyAsync(x => x.Email == request.Email);
            if (exists)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("This email is already associated with an account."));
            }

            var user = new Models.User
            {
                Name = request.Name,
                Email = request.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = 1
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var userDto = new
            {
                user.Id,
                user.Name,
                user.Email,
                role = user.Role
            };

            return Ok(ApiResponse<object>.Ok(new { user = userDto }));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpGet("logout")]
    public ActionResult<ApiResponse<object>> Logout()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(Request.Headers.Authorization))
            {
                return Ok(ApiResponse<object>.Ok(null));
            }

            return BadRequest(ApiResponse<object>.Fail("Validation error."));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail(ex.Message));
        }
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
}
