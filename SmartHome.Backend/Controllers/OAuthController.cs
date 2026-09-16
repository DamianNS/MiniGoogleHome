using Microsoft.AspNetCore.Mvc;
using SmartHome.Backend.Contracts;
using SmartHome.Backend.Services;

namespace SmartHome.Backend.Controllers;

[ApiController]
[Route("oauth")]
public sealed class OAuthController(OAuthTokenService tokenService) : ControllerBase
{
    [HttpPost("token")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Token(
        [FromForm] OAuthTokenRequest request,
        CancellationToken cancellationToken)
    {
        OAuthTokenServiceResult result;
        if (string.Equals(request.GrantType, "refresh_token", StringComparison.Ordinal))
        {
            result = await tokenService.RefreshAsync(request, cancellationToken);
        }
        else
        {
            result = await tokenService.ExchangeAsync(request, cancellationToken);
        }

        return result.IsSuccess
            ? Ok(result.Response)
            : BadRequest(result.Error);
    }
}
