using SmartHome.Backend.Services;
using SmartHome.Shared.Contracts;
using SmartHome.Shared.Entities;

namespace SmartHome.Backend.Commands;

public class SetVolumeCommand(MediaBridgeService mediaBridgeService) : ICommandHome
{
    public async Task<GoogleHomeCommandResponse> Execute(GoogleHomeExecution execution,  List<string> ids, List<MiniDTO> minis, CancellationToken cancellationToken = default)
    {
        var volumeLevel = execution.Params.VolumeLevel;
        var result = volumeLevel is null
            ? new MediaCommandResult(false, "El volumen no es válido.")
            : await mediaBridgeService.SetVolumeAsync(volumeLevel.Value, cancellationToken);
            
        var mini = minis.First();
        var data = (mini.Data != null 
            ? System.Text.Json.JsonSerializer.Deserialize<GoogleHomeState>(mini.Data)
            : null) ?? new GoogleHomeState();
        data.CurrentVolume = volumeLevel ?? 50;
        mini.Data = System.Text.Json.JsonSerializer.Serialize(data);

        return result.Succeeded
            ? new GoogleHomeCommandResponse
            {
                Ids = ids,
                Status = "SUCCESS",
                States = data
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
