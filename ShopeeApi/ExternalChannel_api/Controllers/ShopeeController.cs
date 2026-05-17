using Microsoft.AspNetCore.Mvc;
using ExternalChannel_api.Models;
using ExternalChannel_api.Services;

namespace ExternalChannel_api.Controllers
{
    [ApiController]
    [Route("shopee")]
    public class ShopeeController : ControllerBase
    {
        private readonly ShopeeOrderStore _orderStore;
        private readonly ShopeeOrderProxy _orderProxy;

        public ShopeeController(ShopeeOrderStore orderStore, ShopeeOrderProxy orderProxy)
        {
            _orderStore = orderStore;
            _orderProxy = orderProxy;
        }

        // Lấy danh sách đơn bán
        [HttpGet("orders")]
        [ProducesResponseType(typeof(IReadOnlyCollection<ShopeeOrder>), StatusCodes.Status200OK)]
        public IActionResult GetOrders()
        {
            return Ok(_orderStore.GetAll());
        }

        // Tạo 1 đơn bán
        [HttpPost("orders")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateOrder([FromBody] CreateShopeeOrderRequest request, CancellationToken cancellationToken)
        {
            // giả lập: chỉ forward sang API khác, không xử lý phức tạp ở đây
            var resp = await _orderProxy.ForwardCreateAsync(request, cancellationToken);

            // Trả nguyên trạng status code + body từ API đích
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            return StatusCode((int)resp.StatusCode, body);
        }
    }
}
