using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SmartHome.Backend.Services;
using SmartHome.Shared.Contracts;
using System.Security.Claims;

namespace SmartHome.Backend.Controllers;

[ApiController]
//[Authorize(AuthenticationSchemes = "Bearer")]
[Route("api/smarthome")]
public sealed class SmartHomeController(MediaBridgeService mediaBridgeService, ILogger<SmartHomeController> log) : ControllerBase
{
    private const string DeviceId = "pi_media_speaker_01";
        
    //[ActivatorUtilitiesConstructor]
    //public SmartHomeController(MediaBridgeService mediaBridgeService)
    //{
    //    this.mediaBridgeService = mediaBridgeService;
    //}

    [HttpPost]
    public async Task<IActionResult> Handle(
        [FromBody] GoogleHomeRequest request,
        CancellationToken cancellationToken)
    {
        log.LogInformation("Received request: {RequestId}, Intent: {Intent}", request.RequestId, request.Inputs.FirstOrDefault()?.Intent);
        var intent = request.Inputs.FirstOrDefault()?.Intent;
        return intent switch
        {
            GoogleHomeIntents.Sync => Ok(CreateSyncResponse(request.RequestId)),
            GoogleHomeIntents.Query => await QueryAsync(request, cancellationToken),
            GoogleHomeIntents.Execute => await ExecuteAsync(request, cancellationToken),
            _ => BadRequest(new GoogleHomeErrorResponse
            {
                RequestId = request.RequestId,
                Payload = new GoogleHomeErrorPayload
                {
                    ErrorCode = "unsupported_intent",
                    ErrorMessage = "El intent todavía no está implementado."
                }
            })
        };
    }

    private async Task<IActionResult> ExecuteAsync(
        GoogleHomeRequest request,
        CancellationToken cancellationToken)
    {
        log.LogInformation("Executing commands for request: {RequestId}", request.RequestId);
        if (mediaBridgeService is null || request.Inputs.Count == 0)
        {
            log.LogCritical("MediaBridgeService is not initialized or request inputs are empty for request: {RequestId}", request.RequestId);
            return BadRequest(CreateError(request.RequestId, "invalid_request", "El payload EXECUTE no es válido."));
        }

        var responses = new List<GoogleHomeCommandResponse>();
        foreach (var command in request.Inputs.SelectMany(input => input.Payload.Commands))
        {
            var ids = command.Devices.Select(device => device.Id).ToList();
            if (ids.Count == 0 || ids.Any(id => !string.Equals(id, DeviceId, StringComparison.Ordinal)))
            {
                responses.Add(CreateCommandError(ids, "Dispositivo no soportado."));
                continue;
            }

            foreach (var execution in command.Execution)
            {
                if (string.Equals(execution.Command, GoogleHomeCommands.MediaPlay, StringComparison.Ordinal))
                {
                    var query = execution.Params.MediaQuery?.Query ?? string.Empty;
                    var result = await mediaBridgeService.PlayAsync(query, cancellationToken);
                    responses.Add(result.Succeeded
                        ? new GoogleHomeCommandResponse
                        {
                            Ids = ids,
                            Status = "SUCCESS",
                            States = new GoogleHomeState { PlaybackState = "PLAYING" }
                        }
                        : CreateCommandError(ids, result.Error ?? "No se pudo iniciar la reproducción."));
                }
                else if (string.Equals(execution.Command, GoogleHomeCommands.SetVolume, StringComparison.Ordinal))
                {
                    var volumeLevel = execution.Params.VolumeLevel;
                    var result = volumeLevel is null
                        ? new MediaCommandResult(false, "El volumen no es válido.")
                        : await mediaBridgeService.SetVolumeAsync(volumeLevel.Value, cancellationToken);
                    responses.Add(result.Succeeded
                        ? new GoogleHomeCommandResponse
                        {
                            Ids = ids,
                            Status = "SUCCESS",
                            States = new GoogleHomeState { CurrentVolume = volumeLevel }
                        }
                        : CreateCommandError(ids, result.Error ?? "No se pudo ajustar el volumen."));
                }
                else
                {
                    responses.Add(CreateCommandError(ids, "Comando no soportado."));
                }
            }
        }

        var ret = new GoogleHomeResponse
        {
            RequestId = request.RequestId,
            Payload = new GoogleHomeResponsePayload { Commands = responses }
        };
        log.LogInformation("Execution response for request: {RequestId}, Response: {Response}", request.RequestId, System.Text.Json.JsonSerializer.Serialize(ret));
        return Ok(ret);        
    }

    private async Task<IActionResult> QueryAsync(
        GoogleHomeRequest request,
        CancellationToken cancellationToken)
    {
        var references = request.Inputs
            .SelectMany(input => input.Payload.Devices)
            .ToList();
        var states = new Dictionary<string, GoogleHomeState>();
        foreach (var reference in references)
        {
            if (!string.Equals(reference.Id, DeviceId, StringComparison.Ordinal))
            {
                log.LogInformation($"El dispositivo con ID {reference.Id} no es soportado. Marcando como offline.");
                states[reference.Id] = new GoogleHomeState { Online = false };
                continue;
            }

            var currentVolume = mediaBridgeService is null
                ? null
                : await mediaBridgeService.GetCurrentVolumeAsync(cancellationToken);
            states[DeviceId] = new GoogleHomeState
            {
                Online = true,
                CurrentVolume = currentVolume ?? 100
            };
        }

        var ret = new GoogleHomeQueryResponse
        {
            RequestId = request.RequestId,
            Payload = new GoogleHomeQueryPayload { Devices = states }
        };
        var texto = System.Text.Json.JsonSerializer.Serialize(ret);
        log.LogInformation($"Serialized result QueryAsync: {texto}");
        return Ok(ret);
    }

    private GoogleHomeResponse CreateSyncResponse(string requestId)
    {
        log.LogInformation("Creating SYNC response for request: {RequestId}", requestId);
        var agentUserId = User.FindFirstValue("agent_user_id") ?? string.Empty;

        var ret = new GoogleHomeResponse
        {
            RequestId = requestId,
            Payload = new GoogleHomeResponsePayload
            {
                AgentUserId = agentUserId,
                Devices =
                [
                    new GoogleHomeDevice
                    {
                        Id = DeviceId,
                        Type = "action.devices.types.SPEAKER",
                        Traits =
                        [
                            "action.devices.traits.MediaState",
                            "action.devices.traits.OnOff",
                            "action.devices.traits.TransportControl",
                            "action.devices.traits.Volume"
                        ],
                        Name = new GoogleHomeDeviceName
                        {
                            Name = "Parlante Raspberry",
                            DefaultNames = ["Reproductor de la Pi"],
                            Nicknames = ["Patoclo"]
                        },
                        WillReportState = false,
                        DeviceInfo = new GoogleHomeDeviceInfo
                        {
                            Manufacturer = "Niquel Soft",
                            Model = "PiMediaBridgeV1",
                            HwVersion = "Raspberry Pi",
                            SwVersion = "1.0.0"
                        },
                        
                    }
                ],
                Commands = null
            }
        };

        var texto = System.Text.Json.JsonSerializer.Serialize(ret);
        log.LogInformation($"Serialized result SYNC: {texto}");

        return ret;
    }

    private static GoogleHomeCommandResponse CreateCommandError(
        List<string> ids,
        string errorMessage)
    {
        return new GoogleHomeCommandResponse
        {
            Ids = ids,
            Status = "ERROR",
            States = new GoogleHomeState { Online = false }
        };
    }

    private static GoogleHomeErrorResponse CreateError(
        string requestId,
        string errorCode,
        string errorMessage)
    {
        return new GoogleHomeErrorResponse
        {
            RequestId = requestId,
            Payload = new GoogleHomeErrorPayload
            {
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            }
        };
    }
}
