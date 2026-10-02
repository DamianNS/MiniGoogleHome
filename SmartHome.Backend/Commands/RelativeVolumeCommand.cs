using SmartHome.Backend.Services;
using SmartHome.Shared.Contracts;

namespace SmartHome.Backend.Commands;

public class RelativeVolumeCommand(MediaBridgeService mediaBridgeService)
{
    public async Task<GoogleHomeCommandResponse> Execute(GoogleHomeExecution execution, List<string> ids, CancellationToken cancellationToken = default)
    {
        var relativeSteps = execution.Params.RelativeSteps ?? 0;
        var nuevoVolumen = await mediaBridgeService.SetRelativeVolumeAsync(relativeSteps, cancellationToken);
        
        return new GoogleHomeCommandResponse
        {
            Ids = ids,
            Status = "SUCCESS",
            States = new GoogleHomeState { CurrentVolume = nuevoVolumen }
        };
    }
}
