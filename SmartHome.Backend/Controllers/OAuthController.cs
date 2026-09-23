using Microsoft.AspNetCore.Mvc;
using SmartHome.Backend.Contracts;
using SmartHome.Backend.Services;

namespace SmartHome.Backend.Controllers;

[ApiController]
[Route("oauth")]
public sealed class OAuthController(OAuthTokenService tokenService, ILogger<OAuthController> log) : ControllerBase
{
    [HttpPost("token")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Token(
        [FromForm] OAuthTokenRequest request,
        CancellationToken cancellationToken)
    {
        log.LogInformation($"Received OAuth token request with grant type: {request.GrantType}");
        log.LogInformation($"Request details: ClientId={request.ClientId}, RedirectUri={request.RedirectUri}, Code={request.Code}, RefreshToken={request.RefreshToken}");

        OAuthTokenServiceResult result;
        if (string.Equals(request.GrantType, "refresh_token", StringComparison.Ordinal))
        {
            result = await tokenService.RefreshAsync(request, cancellationToken);
        }
        else
        {
            result = await tokenService.ExchangeAsync(request, cancellationToken);
        }
        
        log.LogInformation($"Token request result: IsSuccess={result.IsSuccess}");
        return result.IsSuccess
            ? Ok(result.Response)
            : BadRequest(result.Error);
    }
}
