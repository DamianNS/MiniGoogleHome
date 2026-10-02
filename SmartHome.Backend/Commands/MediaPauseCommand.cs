

using SmartHome.Backend.Commands;
using SmartHome.Backend.Services;
using SmartHome.Shared.Contracts;
using SmartHome.Shared.Entities;

public class MediaPauseCommand(MediaBridgeService mediaBridgeService) : ICommandHome
{
    public async Task<GoogleHomeCommandResponse> Execute(GoogleHomeExecution execution, List<string> ids, List<MiniDTO> minis, CancellationToken cancellationToken = default)
    {
        await mediaBridgeService.StopAsync(cancellationToken);
        var mini = minis.First();
        var data = (mini.Data != null 
            ? System.Text.Json.JsonSerializer.Deserialize<GoogleHomeState>(mini.Data)
            : null) ?? new GoogleHomeState();
        data.PlaybackState = SmartHome.Shared.Constantes.PlaybackState.PAUSED;
        mini.Data = System.Text.Json.JsonSerializer.Serialize(data);
        return new GoogleHomeCommandResponse
        {
            Ids = ids,
            Status = "SUCCESS",
            States = data
        };
    }
}