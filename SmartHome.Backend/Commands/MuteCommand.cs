using SmartHome.Backend.Commands;
using SmartHome.Backend.Services;
using SmartHome.Shared.Contracts;
using SmartHome.Shared.Entities;

namespace SmartHome.Backend.Commands;

public class MuteCommand(MediaBridgeService mediaBridgeService) : ICommandHome
{
    public async Task<GoogleHomeCommandResponse> Execute(GoogleHomeExecution execution, List<string> ids, List<MiniDTO> minis, CancellationToken cancellationToken=default)
    {
        var mute = execution.Params.Mute ?? true;
        
                
        var mini = minis.First();
        var data = (mini.Data != null 
            ? System.Text.Json.JsonSerializer.Deserialize<GoogleHomeState>(mini.Data)
            : null) ?? new GoogleHomeState();

        if(mute)
        {
            await mediaBridgeService.SetVolumeAsync(0, cancellationToken);            
        }
        else
        {
            await mediaBridgeService.SetVolumeAsync(data.CurrentVolume, cancellationToken);
        }

        data.IsMuted = mute;
        mini.Data = System.Text.Json.JsonSerializer.Serialize(data);

        return new GoogleHomeCommandResponse
        {
            Ids = ids,
            Status = "SUCCESS",
            States = data
        };
    }
}