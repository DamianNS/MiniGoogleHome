using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHome.Backend.Services;

namespace SmartHome.Backend.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/status")]
public sealed class StatusController(MediaBridgeService mediaBridgeService) : ControllerBase
{
    [HttpGet("volume")]
    public async Task<IActionResult> Volume(CancellationToken cancellationToken)
    {
        var currentVolume = await mediaBridgeService.GetCurrentVolumeAsync(cancellationToken);
        return Ok(new { currentVolume });
    }
}
