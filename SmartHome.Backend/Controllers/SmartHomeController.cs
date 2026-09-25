using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHome.Backend.Services;
using SmartHome.Shared.Contracts;
using SmartHome.Shared.Persistence;
using System.Security.Claims;

namespace SmartHome.Backend.Controllers;

[ApiController]
[Authorize]
[Route("api/smarthome")]
public sealed class SmartHomeController(
    MediaBridgeService mediaBridgeService, 
    ILogger<SmartHomeController> log,
    IDbContextFactory<SmartHomeDbContext> dbContextFactory) : ControllerBase
{
    private const string DeviceId = "pi_media_speaker_01";
   
    private async Task<Shared.Entities.Usuario?> GetUser()
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            return null;
        }
        if(!int.TryParse(userIdClaim.Value, out var userId))
        {
            log.LogWarning("Invalid user ID claim value: {UserIdClaimValue}", userIdClaim.Value);
            return null;
        }
        using var context = dbContextFactory.CreateDbContext();
        var user = await context.Usuarios.Include(u => u.Minis).FirstOrDefaultAsync(u => u.Id == userId);
        return user;
    }


    [HttpPost]
    public async Task<IActionResult> Handle(
        [FromBody] GoogleHomeRequest request,
        CancellationToken cancellationToken)
    {
        var user = await GetUser();
        if (user == null) return NotFound("Usuario no encontrado.");

        log.LogInformation("Received request: {RequestId}, Intent: {Intent}", request.RequestId, request.Inputs.FirstOrDefault()?.Intent);
        var intent = request.Inputs.FirstOrDefault()?.Intent;
        return intent switch
        {
            GoogleHomeIntents.Sync => Ok(await CreateSyncResponse(request.RequestId)),
            GoogleHomeIntents.Query => await QueryAsync(request, cancellationToken),
            GoogleHomeIntents.Execute => await ExecuteAsync(request, cancellationToken),
            _ => BadIntentResponse(request.RequestId)
        };
    }

    private IActionResult BadIntentResponse(string requestId)
    {
        log.LogWarning("Bad intent response for request: {RequestId}, Error", requestId);
        return BadRequest(new GoogleHomeErrorResponse
        {
            RequestId = requestId,
            Payload = new GoogleHomeErrorPayload
            {
                ErrorCode = "unsupported_intent",
                ErrorMessage = "El intent todavía no está implementado."
            }
        });
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
        var textorequest = System.Text.Json.JsonSerializer.Serialize(request);
        log.LogInformation($"Serialized textorequest QueryAsync: {textorequest}");

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
                CurrentVolume = currentVolume ?? 100,
                PlaybackState = "PAUSED",
                Status = "SUCCESS",
                ActivityState = "IDLE",
                On = true
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

    private async Task<GoogleHomeResponse> CreateSyncResponse(string requestId)
    {
        log.LogInformation("Creating SYNC response for request: {RequestId}", requestId);
        
        using var context = dbContextFactory.CreateDbContext();
        var user = await GetUser();
        if(user is null)
        {
            log.LogWarning("User not found for SYNC response creation.");
            throw new InvalidOperationException("User not found for SYNC response creation.");
        }

        var agentUserId = user.AgentUserId ?? throw new InvalidOperationException("AgentUserId is null for the user.");
        var devices = user.Minis?.Select(d => new GoogleHomeDevice
        {
            Id = $"pi_media_speaker_{d.Id.ToString("0000")}",
            Type = "action.devices.types.SPEAKER",
            Traits = new List<string>
            {
                "action.devices.traits.MediaState",
                "action.devices.traits.OnOff",
                "action.devices.traits.TransportControl",
                "action.devices.traits.Volume"
            },
            Name = new GoogleHomeDeviceName
            {
                Name = d.Nombre,
                DefaultNames = new List<string> { d.Nombre },
                Nicknames = new List<string> { d.Nombre }
            },
            WillReportState = false,
            DeviceInfo = new GoogleHomeDeviceInfo
            {
                Manufacturer = "Niquel Soft",
                Model = "PiMediaBridgeV1",
                HwVersion = "Raspberry Pi",
                SwVersion = "1.0.0"
            },
            Attributes = new GoogleHomeDeviceAttributes
            {
                volumeMaxLevel = 100,
                volumeCanMuteAndUnmute = true
            }
        }).ToList();

        var ret = new GoogleHomeResponse
        {
            RequestId = requestId,
            Payload = new GoogleHomeResponsePayload
            {
                AgentUserId = agentUserId,
                Devices = devices ?? new List<GoogleHomeDevice>(),
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
