using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartHome.Backend.Controllers;
using SmartHome.Shared.Contracts;

namespace SmartHome.Shared.Tests;

public sealed class QueryContractTests
{
    [Fact]
    public async Task Query_returns_online_state_for_supported_device_and_offline_for_unknown_id()
    {
        var controller = new SmartHomeController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.Handle(new GoogleHomeRequest
        {
            RequestId = "query-1",
            Inputs =
            [
                new GoogleHomeInput
                {
                    Intent = GoogleHomeIntents.Query,
                    Payload = new GoogleHomeInputPayload
                    {
                        Devices =
                        [
                            new GoogleHomeDeviceReference { Id = "pi_media_speaker_01" },
                            new GoogleHomeDeviceReference { Id = "unknown-device" }
                        ]
                    }
                }
            ]
        }, CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result).Value as GoogleHomeQueryResponse;
        Assert.Equal("query-1", response!.RequestId);
        Assert.True(response.Payload.Devices["pi_media_speaker_01"].Online);
        Assert.False(response.Payload.Devices["unknown-device"].Online);
    }
}
