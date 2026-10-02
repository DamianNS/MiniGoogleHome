using SmartHome.Backend.Services;
using SmartHome.Shared.Contracts;

namespace SmartHome.Backend.Commands;

public class MediaPlayCommand(MediaBridgeService mediaBridgeService)
{
    public async Task<GoogleHomeCommandResponse> Execute(GoogleHomeExecution execution, List<string> ids, CancellationToken cancellationToken=default)
    {
        var query = execution.Params.MediaQuery?.Query ?? string.Empty;
        var result = await mediaBridgeService.PlayAsync(query, cancellationToken);
        if (result.Succeeded)
        {
            return new GoogleHomeCommandResponse
            {
                Ids = ids,
                Status = "SUCCESS",
                States = new GoogleHomeState { PlaybackState = "PLAYING" }
            };
        }
        else { 
            return CreateCommandError(ids, result.Error ?? "No se pudo iniciar la reproducción.");
        }        
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

