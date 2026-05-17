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

        public ShopeeController(ShopeeOrderFileStore orderStore)
        {
            _orderStore = orderStore;
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
            return Created($"{Request.PathBase}/shopee/orders/{created.OrderSn}", created);
        }
    }
}
