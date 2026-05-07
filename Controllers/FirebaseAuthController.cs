using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vibra_Dotnet_api.Contracts;
using Vibra_Dotnet_api.Contracts.Requests;

namespace Vibra_Dotnet_api.Controllers;

[ApiController]
[Route("api/auth/firebase")]
public sealed class FirebaseAuthController : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("signin")]
    public ActionResult<ApiResponse<object>> Signin([FromBody] FirebaseSigninRequest request)
    {
        // TODO: Port logic from Laravel `FirebaseAuthController@signin`
        return Ok(ApiResponse<object>.Fail("Not implemented"));
    }
}
