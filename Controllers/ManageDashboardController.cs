using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Accessory_api.Contracts.Responses;
using Accessory_api.Data;

namespace Accessory_api.Controllers;

[ApiController]
[Route("api")]
public sealed class ManageDashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public ManageDashboardController(AppDbContext db)
    {
        _db = db;
    }

    [AllowAnonymous]
    [HttpGet("get-statistics")]
    public async Task<IActionResult> GetStatistics()
    {
        try
        {
            var now = DateTime.Now;
            var month = now.Month;
            var year = now.Year;

            // Keep logic identical to the provided Laravel code.
            var countCustomers = await _db.Database.SqlQuery<int>($"SELECT COUNT(1) AS [Value] FROM users WHERE role = 1").FirstAsync();
            var countProducts = await _db.Database.SqlQuery<int>($"SELECT COUNT(1) AS [Value] FROM products WHERE deleted_at IS NULL").FirstAsync();
            var countOrders = await _db.Database.SqlQuery<int>($"SELECT COUNT(1) AS [Value] FROM bills WHERE status IN (4,5,6) AND MONTH(created_at) = {month} AND YEAR(created_at) = {year}").FirstAsync();
            var countStaff = await _db.Database.SqlQuery<int>($"SELECT COUNT(1) AS [Value] FROM users WHERE role = 2").FirstAsync();

            return Ok(new
            {
                success = true,
                data = new
                {
                    count_customers = countCustomers,
                    count_products = countProducts,
                    count_orders = countOrders,
                    count_staff = countStaff
                }
            });
        }
        catch
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Failed to retrieve statistics."
            });
        }
    }

    [AllowAnonymous]
    [HttpGet("get-revenue-by-year")]
    public async Task<IActionResult> GetRevenueByYear()
    {
        try
        {
            FormattableString sql = $@"
SELECT
    YEAR(created_at) AS [year],
    SUM(total_price) AS [total]
FROM bills
WHERE status = 6
GROUP BY YEAR(created_at)
ORDER BY [year] ASC";

            var revenueByYear = await _db.Database.SqlQuery<RevenueByYearRow>(sql).ToListAsync();

            return Ok(new
            {
                success = true,
                data = revenueByYear
            });
        }
        catch
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Failed to retrieve statistics."
            });
        }
    }

    [AllowAnonymous]
    [HttpGet("get-monthly-revenue")]
    public async Task<IActionResult> GetMonthlyRevenue()
    {
        try
        {
            // Query last 12 months (including current month).
            FormattableString sql = $@"
SELECT
    SUM(total_price) AS [total],
    MONTH(created_at) AS [month],
    YEAR(created_at) AS [year]
FROM bills
WHERE status = 6
  AND created_at >= DATEADD(MONTH, -11, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1))
GROUP BY YEAR(created_at), MONTH(created_at)
ORDER BY [year] ASC, [month] ASC";

            var revenues = await _db.Database.SqlQuery<MonthlyRevenueRow>(sql).ToListAsync();

            var now = DateTime.Now;
            var data = new List<object>(capacity: 12);

            for (var i = 11; i >= 0; i--)
            {
                var dt = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
                var found = revenues.FirstOrDefault(x => x.month == dt.Month && x.year == dt.Year);

                data.Add(new
                {
                    label = $"Tháng {dt:MM}/{dt:yyyy}",
                    total = found?.total ?? 0
                });
            }

            return Ok(new
            {
                success = true,
                data
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = ex.Message
            });
        }
    }
}
