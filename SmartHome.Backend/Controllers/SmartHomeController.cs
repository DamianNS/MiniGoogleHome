using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHome.Backend.Commands;
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
        var textorequest = System.Text.Json.JsonSerializer.Serialize(request);
        log.LogInformation($"api/smarthome request: {textorequest}");

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
        try
        {
            var textorequest = System.Text.Json.JsonSerializer.Serialize(request);
            log.LogInformation($"Serialized textorequest ExecuteAsync: {textorequest}");

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

                var idsNormalizados = ids.Select(id => id.Remove(0, "pi_media_speaker_".Length)).ToList();
                var idsEnteros = new List<int>();
                foreach (var id in idsNormalizados)
                {
                    if (int.TryParse(id, out int idEntero))
                    {
                        idsEnteros.Add(idEntero);
                    }
                    else
                    {
                        log.LogWarning("Invalid device ID format: {DeviceId} for request: {RequestId}", id, request.RequestId);
                        responses.Add(CreateCommandError(ids, $"El ID de dispositivo {id} no es válido."));
                        continue;
                    }
                }

                var context = dbContextFactory.CreateDbContext();
                var minis = await context.Minis.Where(m => idsEnteros.Contains(m.Id)).ToListAsync(cancellationToken);

                if (!minis.Any())
                {
                    responses.Add(CreateCommandError(ids, "Dispositivo no soportado."));
                    continue;
                }

                foreach (var execution in command.Execution)
                {
                    switch (execution.Command) { 
                        case GoogleHomeCommands.MediaPlay:
                        case GoogleHomeCommands.MediaResume:
                            responses.Add(await new MediaPlayCommand(mediaBridgeService).Execute(execution, ids, minis, cancellationToken));
                            break;
                        case GoogleHomeCommands.RelativeVolume:
                            responses.Add(await new RelativeVolumeCommand(mediaBridgeService).Execute(execution, ids, minis, cancellationToken));
                            break;
                        case GoogleHomeCommands.SetVolume:
                            responses.Add(await new SetVolumeCommand(mediaBridgeService).Execute(execution, ids, minis, cancellationToken));
                            break;
                        case GoogleHomeCommands.OnOff:
                            responses.Add(await new OnOffCommand(mediaBridgeService).Execute(execution, ids, minis, cancellationToken));
                            break;
                        case GoogleHomeCommands.Mute:
                            responses.Add(await new MuteCommand(mediaBridgeService).Execute(execution, ids, minis, cancellationToken));
                            break;
                        default:
                            Console.Error.WriteLine($"Comando no implementado: {execution.Command}");
                            responses.Add(CreateCommandError(ids, "Comando no soportado."));
                            break;
                    }                   
                }
                context.SaveChanges();
            }

            var ret = new GoogleHomeResponse
            {
                RequestId = request.RequestId,
                Payload = new GoogleHomeResponsePayload { Commands = responses }
            };
            log.LogInformation("Execution response for request: {RequestId}, Response: {Response}", request.RequestId, System.Text.Json.JsonSerializer.Serialize(ret));
            return Ok(ret);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error executing commands: {ex.ToString()}");
            var errRwet = CreateError(request.RequestId, "99", "Error interno");
            log.LogInformation("Execution Error: {RequestId}, Response: {Response}", request.RequestId, System.Text.Json.JsonSerializer.Serialize(errRwet));
            return Ok(errRwet);
        }        
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

        var user = await GetUser();
        if (user == null) return Unauthorized(CreateError(request.RequestId, "401", "Usuario invalida"));

        using var context = await dbContextFactory.CreateDbContextAsync();

        foreach (var reference in references)
        {
            var ids = reference.Id.Remove(0, "pi_media_speaker_".Length);
            if (!int.TryParse(ids, out int id)) {
                log.LogInformation($"El dispositivo con ID {reference.Id} no es un entero.");
                states[reference.Id] = new GoogleHomeState { Online = false };
                continue;
            }
            var mini = context.Minis.Find(id);
            if(mini == null)
            {
                log.LogInformation($"El dispositivo con ID {reference.Id} no esta en la base de datos.");
                states[reference.Id] = new GoogleHomeState { Online = false };
                continue;
            }

            var data = (mini.Data != null
                ? System.Text.Json.JsonSerializer.Deserialize<GoogleHomeState>(mini.Data)
                : null) ?? new GoogleHomeState();

            var currentVolume = mediaBridgeService is null
                ? null
                : await mediaBridgeService.GetCurrentVolumeAsync(cancellationToken);

            //data.Online = true;
            data.CurrentVolume = currentVolume ?? data.CurrentVolume ?? 100;

            if (mini.Estado != Shared.Constantes.EstadoEnum.Off)
            {
                var playbackState = mini.Estado switch
                {
                    Shared.Constantes.EstadoEnum.Play => Shared.Constantes.PlaybackState.PLAYING,                    
                    _ => Shared.Constantes.PlaybackState.STOPPED
                };

                // data.Online = true;
                // data.CurrentVolume = currentVolume ?? data.CurrentVolume ?? 50;
                // data.PlaybackState = playbackState;
                // data.Status = "SUCCESS";
                // data.ActivityState = Shared.Constantes.ActivityState.STANDBY;
                // data.On = true;
                // data.IsMuted = false;

                states[reference.Id] = data;
                // states[reference.Id] = new GoogleHomeState
                // {
                //     Online = true,
                //     CurrentVolume = currentVolume ?? data?.CurrentVolume ?? 50,
                //     PlaybackState = playbackState,
                //     Status = "SUCCESS",
                //     ActivityState = data?.ActivityState ?? Shared.Constantes.ActivityState.ACTIVE,
                //     On = data?.On ?? true,
                //     IsMuted = data?.IsMuted ?? false,
                // };

                var jsonData = System.Text.Json.JsonSerializer.Serialize(states[reference.Id]);
                log.LogInformation($"Device state for {reference.Id}: {jsonData}");
                mini.Data = jsonData;
                context.SaveChanges();
            }
            else {
                states[reference.Id] = new GoogleHomeState
                {
                    Online = false
                };
            }            
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
            Id = $"pi_media_speaker_{d.Id.ToString("00")}",
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
                Name = "Perlante Rassberry",
                DefaultNames = new List<string> { "Perlante Rassberry" },
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
