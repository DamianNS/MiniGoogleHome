using SmartHome.Backend.Services;
using SmartHome.Shared.Contracts;
using SmartHome.Shared.Entities;

namespace SmartHome.Backend.Commands;

public class RelativeVolumeCommand(MediaBridgeService mediaBridgeService) : ICommandHome
{
    public async Task<GoogleHomeCommandResponse> Execute(GoogleHomeExecution execution,  List<string> ids, List<MiniDTO> minis, CancellationToken cancellationToken = default)
    {
        var relativeSteps = execution.Params.RelativeSteps ?? 0;
        var nuevoVolumen = await mediaBridgeService.SetRelativeVolumeAsync(relativeSteps, cancellationToken);
                
        var mini = minis.First();
        var data = (mini.Data != null 
            ? System.Text.Json.JsonSerializer.Deserialize<GoogleHomeState>(mini.Data)
            : null) ?? new GoogleHomeState();
        data.CurrentVolume = nuevoVolumen;
        mini.Data = System.Text.Json.JsonSerializer.Serialize(data);

        return new GoogleHomeCommandResponse
        {
            Ids = ids,
            Status = "SUCCESS",
            States = data
        };
    }
}
