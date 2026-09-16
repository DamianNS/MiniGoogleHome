using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartHome.Backend.Controllers;
using SmartHome.Shared.Contracts;

namespace SmartHome.Shared.Tests;

public sealed class SmartHomeControllerTests
{
    [Fact]
    public async Task Sync_returns_contractual_speaker_for_authenticated_agent()
    {
        var controller = new SmartHomeController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("agent_user_id", "usr_master_pi_01")],
                    "Bearer"))
                }
            }
        };

        var result = await controller.Handle(new GoogleHomeRequest
        {
            RequestId = "4478392110293811",
            Inputs = [new GoogleHomeInput { Intent = GoogleHomeIntents.Sync }]
        }, CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result).Value as GoogleHomeResponse;
        var device = Assert.Single(response!.Payload.Devices);
        Assert.Equal("4478392110293811", response.RequestId);
        Assert.Equal("usr_master_pi_01", response.Payload.AgentUserId);
        Assert.Equal("pi_media_speaker_01", device.Id);
        Assert.Equal("action.devices.types.SPEAKER", device.Type);
        Assert.Contains("action.devices.traits.MediaState", device.Traits);
        Assert.Contains("action.devices.traits.Volume", device.Traits);
        Assert.Equal("Niquelsoft", device.DeviceInfo.Manufacturer);
    }
}
