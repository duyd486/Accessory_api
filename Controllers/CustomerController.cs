using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vibra_Dotnet_api.Data;

namespace Vibra_Dotnet_api.Controllers;

[ApiController]
[Route("api")]
public sealed class CustomerController : ControllerBase
{
    private readonly AppDbContext _db;

    public CustomerController(AppDbContext db)
    {
        _db = db;
    }

    [AllowAnonymous]
    [HttpGet("list-customer")]
    public async Task<IActionResult> ListCustomer()
    {
        var users = await _db.Users.AsNoTracking()
            .Select(u => new
            {
                id = u.Id,
                role = u.Role,
                name = u.Name,
                email = u.Email,
                avatar = u.Avatar,
                phone = u.Phone,
                address = u.Address
            })
            .ToListAsync();

        return Ok(new
        {
            status = true,
            data = users
        });
    }
}
