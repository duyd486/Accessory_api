using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Accessory_api.Contracts;
using Accessory_api.Contracts.Requests;
using Accessory_api.Data;
using Accessory_api.Models;
using Accessory_api.Services;
using QRCoder;

namespace Accessory_api.Controllers;

[ApiController]
[Route("api")]
public sealed class PaymentController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPayOSService _payOS;

    private const long STORE_CHANNEL_ID = 1;

    public PaymentController(AppDbContext db, IPayOSService payOS)
    {
        _db = db;
        _payOS = payOS;
    }

    [AllowAnonymous]
    [HttpGet("channels")]
    public async Task<ActionResult<ApiResponse<object>>> GetChannels()
    {
        try
        {
            var channels = await _db.Channels.AsNoTracking()
                .Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    type = c.Type
                })
                .ToListAsync();
            return Ok(ApiResponse<object>.Ok(new { channels }));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail(ex.Message));
        }
    }

    [Authorize]
    [HttpPost("create-bill")]
    public async Task<ActionResult<ApiResponse<object>>> CreateBill([FromBody] CreateBillRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId is null)
            {
                return Unauthorized(ApiResponse<object>.Fail("Unauthenticated."));
            }

            if (string.IsNullOrWhiteSpace(request.PaymentMethod)
                || request.TotalPrice is null
                || string.IsNullOrWhiteSpace(request.Phone)
                || string.IsNullOrWhiteSpace(request.Address)
                || request.Items is null
                || request.Items.Count == 0)
            {
                return BadRequest(ApiResponse<object>.Fail("Validation error."));
            }

            var channelId = request.ChannelId ?? request.ChannelIdSnake;

            var paymentMethod = IsOnlinePaymentMethod(request.PaymentMethod)
                ? Bill.PAYMENT_METHOD_ONLINE
                : Bill.PAYMENT_METHOD_OFFLINE;

            var orderCode = GenerateOrderCode();
            var now = DateTime.UtcNow;

            await using var tx = await _db.Database.BeginTransactionAsync();

            var bill = new Bill
            {
                UserId = userId.Value,
                OrderCode = orderCode.ToString(),
                PaymentMethod = paymentMethod,
                TotalPrice = request.TotalPrice,
                Phone = request.Phone,
                Address = request.Address,
                Status = null,
                CreatedAt = now,
                UpdatedAt = now,
                ChannelId = channelId,
            };

            _db.Bills.Add(bill);
            await _db.SaveChangesAsync();

            var itemIds = request.Items.Select(x => x.Id).Distinct().ToList();
            var productNames = await _db.Products.AsNoTracking()
                .Where(p => itemIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name })
                .ToDictionaryAsync(x => x.Id, x => x.Name);

            var payItems = new List<PayOSItem>(request.Items.Count);

            foreach (var item in request.Items)
            {
                _db.BillDetails.Add(new BillDetail
                {
                    BillId = bill.Id,
                    ProductId = item.Id,
                    Quantity = item.Quantity,
                    TotalPrice = item.TotalPrice,
                    CreatedAt = now,
                    UpdatedAt = now
                });

                productNames.TryGetValue(item.Id, out var name);
                payItems.Add(new PayOSItem(name ?? string.Empty, item.Quantity, item.Price));
            }

            await _db.SaveChangesAsync();

            string? checkoutUrl = null;
            string? qrImageUrl = null;

            if (paymentMethod == Bill.PAYMENT_METHOD_ONLINE)
            {
                bill.Status = Bill.STATUS_PROCESSING;
                bill.UpdatedAt = now;
                await _db.SaveChangesAsync();

                var res = await _payOS.CreatePaymentLinkAsync(orderCode, request.TotalPrice.Value, payItems);
                if (res is null)
                {
                    await tx.RollbackAsync();
                    return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail("Không th? t?o link thanh toán online"));
                }

                checkoutUrl = res.CheckoutUrl;

                if (channelId == STORE_CHANNEL_ID)
                {
                    qrImageUrl = await SaveQrToUploadsAsync(orderCode, res.qrCode);
                    if(qrImageUrl == null)
                    {
                        qrImageUrl = checkoutUrl; // fallback to checkout URL if QR generation fails
                    }
                }
            }
            else
            {
                bill.Status = Bill.STATUS_PREPARING;
                bill.UpdatedAt = now;
                await _db.SaveChangesAsync();
            }

            await tx.CommitAsync();

            var billDto = new
            {
                id = bill.Id,
                user_id = bill.UserId,
                order_code = bill.OrderCode,
                payment_method = bill.PaymentMethod,
                total_price = bill.TotalPrice,
                phone = bill.Phone,
                address = bill.Address,
                status = bill.Status,
                channel_id = bill.ChannelId,
                checkout_url = checkoutUrl,
                qr_image_url = qrImageUrl
            };

            return Ok(ApiResponse<object>.Ok(new { bill = billDto }));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail(ex.Message));
        }
    }

    private async Task<string?> SaveQrToUploadsAsync(int orderCode, string qrCodeString)
    {
        try
        {
            var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            var tempQrsDir = Path.Combine(uploadsRoot, "tempqrs");
            Directory.CreateDirectory(tempQrsDir);


            var generator = new QRCodeGenerator();
            var data = generator.CreateQrCode(qrCodeString, QRCodeGenerator.ECCLevel.Q);
            var qr = new PngByteQRCode(data);

            byte[] bytes = qr.GetGraphic(20);

            var fileName = $"payos_{orderCode}.png";
            var filePath = Path.Combine(tempQrsDir, fileName);
            await System.IO.File.WriteAllBytesAsync(filePath, bytes);

            return $"http://localhost:8000/uploads/tempqrs/{fileName}";
        }
        catch
        {
            return null;
        }
    }

    [Authorize]
    [HttpGet("check-payment-status")]
    public async Task<ActionResult<ApiResponse<object>>> CheckPaymentStatus([FromQuery(Name = "orderCode")] int? orderCode)
    {
        if (orderCode is null)
        {
            return BadRequest(ApiResponse<object>.Fail("Validation error."));
        }

        try
        {
            var data = await _payOS.GetPaymentStatusAsync(orderCode.Value);
            if (data is null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail("Internal Server Error"));
            }

            var bill = await _db.Bills.FirstOrDefaultAsync(x => x.OrderCode == orderCode.Value.ToString());
            if (bill is not null)
            {
                bill.Status = data.Status switch
                {
                    "PAID" => Bill.STATUS_PAID,
                    "PENDING" => Bill.STATUS_PENDING,
                    "PROCESSING" => Bill.STATUS_PROCESSING,
                    _ => Bill.STATUS_CANCELLED
                };
                bill.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return Ok(ApiResponse<object>.Ok(new { status = data.Status }));
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

    private static int GenerateOrderCode()
    {
        // Similar to Laravel's microtime-based 6-digit generation.
        var value = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return (int)(value % 1_000_000);
    }

    private static bool IsOnlinePaymentMethod(string? paymentMethod)
    {
        if (string.IsNullOrWhiteSpace(paymentMethod))
        {
            return false;
        }

        return paymentMethod.Equals("online", StringComparison.OrdinalIgnoreCase)
            || paymentMethod.Equals("Chuyển khoản", StringComparison.OrdinalIgnoreCase)
            || paymentMethod.Equals("Chuyen khoan", StringComparison.OrdinalIgnoreCase);
    }
}
