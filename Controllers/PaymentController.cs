using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vibra_Dotnet_api.Contracts;
using Vibra_Dotnet_api.Contracts.Requests;

namespace Vibra_Dotnet_api.Controllers;

[ApiController]
[Route("api")]
public sealed class PaymentController : ControllerBase
{
    [Authorize]
    [HttpPost("create-bill")]
    public ActionResult<ApiResponse<object>> CreateBill([FromBody] CreateBillRequest request)
    {
        // TODO: Port logic from Laravel `PaymentController@createBill`
        return Ok(ApiResponse<object>.Fail("Not implemented"));
    }

    [Authorize]
    [HttpGet("check-payment-status")]
    public ActionResult<ApiResponse<object>> CheckPaymentStatus([FromQuery] string? billId)
    {
        // TODO: Port logic from Laravel `PaymentController@checkPaymentStatus`
        return Ok(ApiResponse<object>.Fail("Not implemented"));
    }
}
