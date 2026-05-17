using Microsoft.AspNetCore.Mvc;
using ExternalChannel_api.Models;
using ExternalChannel_api.Services;

namespace ExternalChannel_api.Controllers
{
    [ApiController]
    [Route("shopee")]
    public class ShopeeController : ControllerBase
    {
        private readonly ShopeeOrderFileStore _orderStore;
        private readonly ShopeeUpsertOrderClient _upsertClient;

        public ShopeeController(ShopeeOrderFileStore orderStore, ShopeeUpsertOrderClient upsertClient)
        {
            _orderStore = orderStore;
            _upsertClient = upsertClient;
        }

        // Lấy danh sách đơn bán
        [HttpGet("orders")]
        [ProducesResponseType(typeof(IReadOnlyCollection<ShopeeOrder>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetOrders(CancellationToken cancellationToken)
        {
            return Ok(await _orderStore.GetAllAsync(cancellationToken));
        }

        // Tạo 1 đơn bán
        [HttpPost("orders")]
        [ProducesResponseType(typeof(ShopeeOrder), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateOrder([FromBody] CreateShopeeOrderRequest request, CancellationToken cancellationToken)
        {
            var created = await _orderStore.CreateAsync(request, cancellationToken);

            var upsertRequest = new UpsertShopeeOrderRequest(
                OrderSn: created.OrderSn,
                BuyerName: created.BuyerName,
                BuyerPhone: created.BuyerPhone,
                ShippingAddress: created.ShippingAddress,
                TotalAmount: (double)created.TotalAmount,
                Currency: created.Currency,
                Status: created.Status,
                CreatedAt: created.CreatedAt,
                Items: created.Items
                    .Select(i => new UpsertShopeeOrderItemRequest(
                        Sku: i.Sku,
                        Name: i.Name,
                        Quantity: i.Quantity,
                        UnitPrice: (double)i.UnitPrice
                    ))
                    .ToList()
            );

            _ = await _upsertClient.UpsertAsync(upsertRequest, cancellationToken);
            return Created($"{Request.PathBase}/shopee/orders/{created.OrderSn}", created);
        }
    }
}
