using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Net.Http.Json;
using Accessory_api.Contracts.Requests;
using Accessory_api.Contracts.Responses;
using Accessory_api.Data;
using Accessory_api.Models;

namespace Accessory_api.Controllers;

[ApiController]
[Route("api")]
public sealed class ManageOrderController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;

    public ManageOrderController(AppDbContext db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

    [AllowAnonymous]
    [HttpPost("shopee/sync-orders")]
    public async Task<IActionResult> SyncShopeeOrders(CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var orders = await client.GetFromJsonAsync<List<ShopeeOrderDto>>("http://localhost:8001/api/shopee/orders", cancellationToken);

            if (orders is null || orders.Count == 0)
            {
                return Ok(new { message = "Không có đơn Shopee để đồng bộ", synced = 0, created = 0, updated = 0, failed = 0, errors = Array.Empty<object>() });
            }

            var shopeeChannelId = await _db.Channels.AsNoTracking()
                .Where(c => c.Name != null && c.Name.ToLower() == "shopee")
                .Select(c => c.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (shopeeChannelId is null || shopeeChannelId == 0)
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, new { message = "Chưa cấu hình channel Shopee trong bảng channels." });
            }

            var created = 0;
            var updated = 0;
            var failed = 0;
            var errors = new List<object>();

            foreach (var order in orders)
            {
                if (order is null || string.IsNullOrWhiteSpace(order.OrderSn))
                {
                    failed++;
                    errors.Add(new { order_code = (string?)null, message = "Thiếu orderSn" });
                    continue;
                }

                var res = await UpsertShopeeOrderFromExternalAsync(order, shopeeChannelId.Value, cancellationToken);
                if (res.Success)
                {
                    if (res.Created)
                    {
                        created++;
                    }
                    else
                    {
                        updated++;
                    }
                }
                else
                {
                    failed++;
                    errors.Add(new { order_code = order.OrderSn, message = res.Error });
                }
            }

            return Ok(new
            {
                message = "Đồng bộ đơn Shopee thành công",
                synced = created + updated,
                created,
                updated,
                failed,
                errors
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Không thể đồng bộ đơn Shopee", error = ex.Message });
        }
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
                        LEFT JOIN users ON users.id = bills.user_id
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
    [HttpGet("shopee/orders")]
    public async Task<IActionResult> GetShopeeOrders(CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var orders = await client.GetFromJsonAsync<List<ShopeeOrderDto>>("http://localhost:8001/api/shopee/orders", cancellationToken);

            long? shopeeChannelId = await _db.Channels.AsNoTracking()
                .Where(c => c.Name != null && c.Name.ToLower() == "shopee")
                .Select(c => c.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (shopeeChannelId == 0)
            {
                shopeeChannelId = null;
            }

            var data = (orders ?? new List<ShopeeOrderDto>())
                .Select(o => new OrderBillDto(
                    id: 0,
                    order_code: o.OrderSn,
                    user_id: 0,
                    user_name: o.BuyerName,
                    status: MapShopeeStatusToBillStatus(o.Status),
                    created_at: o.CreatedAt?.UtcDateTime,
                    total_price: o.TotalAmount,
                    channel_id: shopeeChannelId,
                    products: (o.Items ?? new List<ShopeeOrderItemDto>())
                        .Select(i => new OrderBillProductDto(
                            product_id: TryParseProductIdFromSku(i.Sku, out var pid) ? pid : 0,
                            product_name: i.Name,
                            quantity: i.Quantity))
                        .ToList()))
                .OrderByDescending(x => x.created_at)
                .ToList();

            return Ok(new { data });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Không thể lấy đơn hàng Shopee", error = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("shopee/upsert-order")]
    public async Task<IActionResult> UpsertShopeeOrder([FromBody] UpsertShopeeOrderRequest request)
    {
        if (request is null)
        {
            return BadRequest(new { message = "Validation error." });
        }

        if (string.IsNullOrWhiteSpace(request.OrderSn)
            || string.IsNullOrWhiteSpace(request.BuyerPhone)
            || string.IsNullOrWhiteSpace(request.ShippingAddress)
            || request.Items is null
            || request.Items.Count == 0)
        {
            return BadRequest(new { message = "Validation error." });
        }

        var now = DateTime.UtcNow;
        var orderSn = request.OrderSn.Trim();
        var userId = 0L; // đơn shopee

        var normalizedStatus = (request.Status ?? string.Empty).Trim().ToUpperInvariant();
        int status;
        if (normalizedStatus == "CANCELLED" || normalizedStatus == "CANCELED")
        {
            status = Bill.STATUS_CANCELLED;
        }
        else if (normalizedStatus == "CONFIRMED")
        {
            status = Bill.STATUS_PREPARING;
        }
        else if (normalizedStatus == "PROCESSING")
        {
            status = Bill.STATUS_PROCESSING;
        }
        else if (normalizedStatus == "SHIPPING")
        {
            status = Bill.STATUS_SHIPPING;
        }
        else if (normalizedStatus == "DONE" || normalizedStatus == "COMPLETED")
        {
            status = Bill.STATUS_DONE;
        }
        else
        {
            status = Bill.STATUS_PREPARING;
        }

        DateTime? createdAt = request.CreatedAt?.UtcDateTime;

        long? shopeeChannelId = await _db.Channels.AsNoTracking()
            .Where(c => c.Name != null && c.Name.ToLower() == "shopee")
            .Select(c => c.Id)
            .FirstOrDefaultAsync();

        if (shopeeChannelId is null || shopeeChannelId == 0)
        {
            return StatusCode(StatusCodes.Status422UnprocessableEntity, new { message = "Chưa cấu hình channel Shopee trong bảng channels." });
        }

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var bill = await _db.Bills.FirstOrDefaultAsync(x => x.OrderCode == orderSn);
            var isCreate = bill is null;
            if (bill is null)
            {
                bill = new Bill
                {
                    CreatedAt = createdAt ?? now
                };
                _db.Bills.Add(bill);
            }

            bill.UserId = userId;
            bill.OrderCode = orderSn;
            bill.Phone = request.BuyerPhone;
            bill.Address = request.ShippingAddress;
            bill.TotalPrice = request.TotalAmount;
            bill.Status = status;
            bill.PaymentMethod = Bill.PAYMENT_METHOD_OFFLINE;
            bill.ChannelId = shopeeChannelId;
            bill.UpdatedAt = now;
            if (!isCreate && createdAt is not null)
            {
                bill.CreatedAt = createdAt;
            }

            await _db.SaveChangesAsync();

            // reset details
            var existingDetails = await _db.BillDetails.Where(d => d.BillId == bill.Id).ToListAsync();
            if (existingDetails.Count > 0)
            {
                _db.BillDetails.RemoveRange(existingDetails);
                await _db.SaveChangesAsync();
            }

            foreach (var item in request.Items)
            {
                if (item is null || string.IsNullOrWhiteSpace(item.Sku))
                {
                    continue;
                }

                if (!TryParseProductIdFromSku(item.Sku, out var productId))
                {
                    await tx.RollbackAsync();
                    return StatusCode(StatusCodes.Status422UnprocessableEntity, new { message = $"SKU không hợp lệ: {item.Sku}. Kỳ vọng dạng SKU-(id sản phẩm)." });
                }

                var existsProduct = await _db.Products.AsNoTracking().AnyAsync(p => p.Id == productId);
                if (!existsProduct)
                {
                    await tx.RollbackAsync();
                    return StatusCode(StatusCodes.Status422UnprocessableEntity, new { message = $"Không tìm thấy sản phẩm với id={productId} (từ SKU={item.Sku})." });
                }

                _db.BillDetails.Add(new BillDetail
                {
                    BillId = bill.Id,
                    ProductId = productId,
                    Quantity = item.Quantity,
                    TotalPrice = item.UnitPrice * item.Quantity,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new
            {
                message = isCreate ? "Tạo đơn Shopee thành công" : "Cập nhật đơn Shopee thành công",
                data = new
                {
                    bill_id = bill.Id,
                    order_code = bill.OrderCode,
                    channel_id = bill.ChannelId,
                    status = bill.Status,
                    total_price = bill.TotalPrice
                }
            });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi hệ thống", error = ex.Message });
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

    private static bool TryParseProductIdFromSku(string sku, out long productId)
    {
        productId = 0;
        if (string.IsNullOrWhiteSpace(sku))
        {
            return false;
        }

        // Expected: SKU-(id sản phẩm)
        var raw = sku.Trim();
        const string prefix = "SKU-";
        if (!raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var idPart = raw[prefix.Length..];
        return long.TryParse(idPart, out productId);
    }

    private static int MapShopeeStatusToBillStatus(string? status)
    {
        var normalizedStatus = (status ?? string.Empty).Trim().ToUpperInvariant();
        if (normalizedStatus == "CANCELLED" || normalizedStatus == "CANCELED")
        {
            return Bill.STATUS_CANCELLED;
        }
        if (normalizedStatus == "CONFIRMED")
        {
            return Bill.STATUS_PREPARING;
        }
        if (normalizedStatus == "PROCESSING")
        {
            return Bill.STATUS_PROCESSING;
        }
        if (normalizedStatus == "SHIPPING")
        {
            return Bill.STATUS_SHIPPING;
        }
        if (normalizedStatus == "DONE" || normalizedStatus == "COMPLETED")
        {
            return Bill.STATUS_DONE;
        }

        return Bill.STATUS_PREPARING;
    }

    private async Task<UpsertShopeeResult> UpsertShopeeOrderFromExternalAsync(ShopeeOrderDto order, long shopeeChannelId, CancellationToken cancellationToken)
    {
        var orderSn = order.OrderSn.Trim();
        if (string.IsNullOrWhiteSpace(orderSn))
        {
            return new UpsertShopeeResult(false, false, "Thiếu orderSn");
        }

        if (order.Items is null || order.Items.Count == 0)
        {
            return new UpsertShopeeResult(false, false, "Thiếu items");
        }

        var now = DateTime.UtcNow;
        var createdAt = order.CreatedAt?.UtcDateTime ?? now;

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var bill = await _db.Bills.FirstOrDefaultAsync(x => x.ChannelId == shopeeChannelId && x.OrderCode == orderSn, cancellationToken);
            var isCreate = bill is null;
            if (bill is null)
            {
                bill = new Bill
                {
                    CreatedAt = createdAt
                };
                _db.Bills.Add(bill);
            }

            bill.UserId = 0;
            bill.OrderCode = orderSn;
            bill.PaymentMethod = Bill.PAYMENT_METHOD_OFFLINE;
            bill.TotalPrice = order.TotalAmount;
            bill.Phone = order.BuyerPhone;
            bill.Address = order.ShippingAddress;
            bill.Status = MapShopeeStatusToBillStatus(order.Status);
            bill.ChannelId = shopeeChannelId;
            bill.UpdatedAt = now;
            if (!isCreate)
            {
                bill.CreatedAt = createdAt;
            }

            await _db.SaveChangesAsync(cancellationToken);

            var existingDetails = await _db.BillDetails
                .Where(d => d.BillId == bill.Id)
                .ToListAsync(cancellationToken);

            if (existingDetails.Count > 0)
            {
                _db.BillDetails.RemoveRange(existingDetails);
                await _db.SaveChangesAsync(cancellationToken);
            }

            foreach (var item in order.Items)
            {
                if (item is null)
                {
                    continue;
                }

                if (!TryParseProductIdFromSku(item.Sku, out var productId))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return new UpsertShopeeResult(false, false, $"SKU không hợp lệ: {item.Sku}. Kỳ vọng dạng SKU-(id sản phẩm)." );
                }

                var existsProduct = await _db.Products.AsNoTracking().AnyAsync(p => p.Id == productId, cancellationToken);
                if (!existsProduct)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return new UpsertShopeeResult(false, false, $"Không tìm thấy sản phẩm với id={productId} (từ SKU={item.Sku}).");
                }

                _db.BillDetails.Add(new BillDetail
                {
                    BillId = bill.Id,
                    ProductId = productId,
                    Quantity = item.Quantity,
                    TotalPrice = item.UnitPrice * item.Quantity,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return new UpsertShopeeResult(true, isCreate, null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return new UpsertShopeeResult(false, false, ex.Message);
        }
    }

    private sealed record OrderBillProductDto(long product_id, string? product_name, int quantity);

    private sealed record OrderBillDto(
        long id,
        string? order_code,
        long user_id,
        string? user_name,
        int status,
        DateTime? created_at,
        double? total_price,
        long? channel_id,
        List<OrderBillProductDto> products);

    public sealed record UpsertShopeeOrderItemRequest(
        string Sku,
        string? Name,
        int Quantity,
        double UnitPrice);

    public sealed record UpsertShopeeOrderRequest(
        string OrderSn,
        string? BuyerName,
        string? BuyerPhone,
        string? ShippingAddress,
        double TotalAmount,
        string? Currency,
        string? Status,
        DateTimeOffset? CreatedAt,
        List<UpsertShopeeOrderItemRequest>? Items);

    private sealed record ShopeeOrderItemDto(
        string Sku,
        string? Name,
        int Quantity,
        double UnitPrice);

    private sealed record ShopeeOrderDto(
        string OrderSn,
        string? BuyerName,
        string? BuyerPhone,
        string? ShippingAddress,
        double TotalAmount,
        string? Currency,
        string? Status,
        DateTimeOffset? CreatedAt,
        List<ShopeeOrderItemDto>? Items);

    private sealed record UpsertShopeeResult(bool Success, bool Created, string? Error);
}
