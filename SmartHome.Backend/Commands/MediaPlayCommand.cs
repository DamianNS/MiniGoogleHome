using SmartHome.Backend.Services;
using SmartHome.Shared.Constantes;
using SmartHome.Shared.Contracts;
using SmartHome.Shared.Entities;

namespace SmartHome.Backend.Commands;

public class MediaPlayCommand(MediaBridgeService mediaBridgeService) : ICommandHome
{
    public async Task<GoogleHomeCommandResponse> Execute(GoogleHomeExecution execution, List<string> ids, List<MiniDTO> minis, CancellationToken cancellationToken=default)
    {
        var query = execution.Params.MediaQuery?.Query ?? string.Empty;
        var result = await mediaBridgeService.PlayAsync(query, cancellationToken);
                     
        var mini = minis.First();
        var data = (mini.Data != null 
            ? System.Text.Json.JsonSerializer.Deserialize<GoogleHomeState>(mini.Data)
            : null) ?? new GoogleHomeState();
        data.PlaybackState = PlaybackState.PLAYING;
        mini.Data = System.Text.Json.JsonSerializer.Serialize(data);

        if (result.Succeeded)
        {
            return new GoogleHomeCommandResponse
            {
                Ids = ids,
                Status = "SUCCESS",
                States = data
            };
        }
        else { 
            return ICommandHome.CreateCommandError(ids, result.Error ?? "No se pudo iniciar la reproducción.");
        }        
    }
}

