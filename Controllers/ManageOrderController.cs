using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Accessory_api.Contracts.Requests;
using Accessory_api.Contracts.Responses;
using Accessory_api.Data;

namespace Accessory_api.Controllers;

[ApiController]
[Route("api")]
public sealed class ManageOrderController : ControllerBase
{
    private readonly AppDbContext _db;

    public ManageOrderController(AppDbContext db)
    {
        _db = db;
    }

    [AllowAnonymous]
    [HttpGet("list-orders")]
    public async Task<IActionResult> ListOrder()
    {
        try
        {
            // Equivalent of the Laravel query builder joins + group by bill.
            FormattableString sql = $@"
                        SELECT
                            bills.id AS id,
                            bills.order_code AS order_code,
                            bills.user_id AS user_id,
                            bills.status AS status,
                            bills.created_at AS created_at,
                            bills.total_price AS total_price,
                            users.name AS user_name,
                            products.id AS product_id,
                            products.name AS product_name,
                            bill_details.quantity AS quantity,
                            bills.channel_id AS channel_id
                        FROM bills
                        INNER JOIN users ON users.id = bills.user_id
                        INNER JOIN bill_details ON bill_details.bill_id = bills.id
                        INNER JOIN products ON products.id = bill_details.product_id
                        ORDER BY bills.created_at DESC";

            var rows = await _db.Database.SqlQuery<OrderListRow>(sql).ToListAsync();

            var bills = new Dictionary<long, OrderBillDto>();

            foreach (var row in rows)
            {
                if (!bills.TryGetValue(row.id, out var bill))
                {
                    bill = new OrderBillDto(
                        id: row.id,
                        order_code: row.order_code,
                        user_id: row.user_id,
                        user_name: row.user_name,
                        status: row.status,
                        channel_id: row.channel_id,
                        created_at: row.created_at,
                        total_price: row.total_price,
                        products: new List<OrderBillProductDto>());
                    bills[row.id] = bill;
                }

                bill.products.Add(new OrderBillProductDto(
                    product_id: row.product_id,
                    product_name: row.product_name,
                    quantity: row.quantity));
            }

            return Ok(new { data = bills.Values.ToList() });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("update-order-status")]
    public async Task<IActionResult> UpdateOrderStatus([FromBody] UpdateOrderStatusRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.OrderId is null)
        {
            errors["order_id"] = new[] { "The order_id field is required." };
        }

        if (request.Status is null)
        {
            errors["status"] = new[] { "The status field is required." };
        }
        //else if (request.Status is not (3 or 4 or 5 or 6))
        //{
        //    errors["status"] = new[] { "The selected status is invalid." };
        //}

        if (errors.Count > 0)
        {
            return StatusCode(StatusCodes.Status422UnprocessableEntity, new { message = "D? li?u không h?p l?.", errors });
        }

        try
        {
            var exists = await _db.Database.SqlQuery<int>($"SELECT COUNT(1) AS [Value] FROM bills WHERE id = {request.OrderId!.Value}")
                .FirstAsync();

            if (exists <= 0)
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { message = "D? li?u không h?p l?.", errors = new { order_id = new[] { "The selected order_id is invalid." } } });
            }

            var now = DateTime.UtcNow;
            await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE bills SET status = {request.Status!.Value}, updated_at = {now} WHERE id = {request.OrderId!.Value}");

            return Ok(new { message = "C?p nh?t tr?ng thái ??n hàng thành công" });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "C?p nh?t tr?ng thái th?t b?i", error = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("send-feedback")]
    public async Task<IActionResult> SendFeedback([FromBody] SendFeedbackRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.BillId is null)
        {
            errors["bill_id"] = new[] { "The bill_id field is required." };
        }

        if (request.Score is null)
        {
            errors["score"] = new[] { "The score field is required." };
        }
        else if (request.Score is < 1 or > 5)
        {
            errors["score"] = new[] { "The score must be between 1 and 5." };
        }

        if (errors.Count > 0)
        {
            return StatusCode(StatusCodes.Status422UnprocessableEntity, new { message = "D? li?u không h?p l?.", errors });
        }

        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { message = "Unauthenticated." });
        }

        try
        {
            var billExists = await _db.Database.SqlQuery<int>($"SELECT COUNT(1) AS [Value] FROM bills WHERE id = {request.BillId!.Value}")
                .FirstAsync();

            if (billExists <= 0)
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { message = "D? li?u không h?p l?.", errors = new { bill_id = new[] { "The selected bill_id is invalid." } } });
            }

            var alreadyRated = await _db.Database.SqlQuery<int>($"SELECT COUNT(1) AS [Value] FROM feedbacks WHERE user_id = {userId.Value} AND bill_id = {request.BillId!.Value} AND type = 0")
                .FirstAsync();

            if (alreadyRated > 0)
            {
                return BadRequest(new { message = "B?n ?ã ?ánh giá ??n hàng này r?i." });
            }

            var now = DateTime.UtcNow;
            await _db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO feedbacks (type, bill_id, user_id, score, comment, status, created_at, updated_at)
VALUES (0, {request.BillId!.Value}, {userId.Value}, {request.Score!.Value}, {request.Comment}, 1, {now}, {now})");

            return Ok(new { message = "G?i ?ánh giá thành công!" });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "L?i h? th?ng", error = ex.Message });
        }
    }

    private long? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(raw, out var id) ? id : null;
    }

    private sealed record OrderBillProductDto(long product_id, string? product_name, int quantity);

    private sealed record OrderBillDto(
        long id,
        int? order_code,
        long user_id,
        string? user_name,
        int status,
        DateTime? created_at,
        double? total_price,
        long? channel_id,
        List<OrderBillProductDto> products);
}
