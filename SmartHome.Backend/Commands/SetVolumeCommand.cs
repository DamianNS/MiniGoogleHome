using SmartHome.Backend.Services;
using SmartHome.Shared.Contracts;

namespace SmartHome.Backend.Commands;

public class SetVolumeCommand(MediaBridgeService mediaBridgeService)
{
    public async Task<GoogleHomeCommandResponse> Execute(GoogleHomeExecution execution, List<string> ids, CancellationToken cancellationToken = default)
    {
        var volumeLevel = execution.Params.VolumeLevel;
        var result = volumeLevel is null
            ? new MediaCommandResult(false, "El volumen no es válido.")
            : await mediaBridgeService.SetVolumeAsync(volumeLevel.Value, cancellationToken);
            
        return result.Succeeded
            ? new GoogleHomeCommandResponse
            {
                Ids = ids,
                Status = "SUCCESS",
                States = new GoogleHomeState { CurrentVolume = volumeLevel }
            }
            : CreateCommandError(ids, result.Error ?? "No se pudo ajustar el volumen.");
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
}
