using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vibra_Dotnet_api.Contracts;

namespace Vibra_Dotnet_api.Controllers;

[ApiController]
[Route("api/auth/google")]
public sealed class GoogleAuthController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("redirect")]
    public ActionResult Redirect()
    {
        // TODO: Port logic from Laravel `GoogleAuthController@redirect`
        return Ok(ApiResponse<object>.Fail("Not implemented"));
    }

    [AllowAnonymous]
    [HttpGet("callback")]
    public ActionResult Callback()
    {
        // TODO: Port logic from Laravel `GoogleAuthController@callback`
        return Ok(ApiResponse<object>.Fail("Not implemented"));
    }

    [AllowAnonymous]
    [HttpGet("get-token")]
    public ActionResult GetToken()
    {
        // TODO: Port logic from Laravel `GoogleAuthController@getToken`
        return Ok(ApiResponse<object>.Fail("Not implemented"));
    }
}
