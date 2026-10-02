using Microsoft.EntityFrameworkCore;
using SmartHome.Backend.Services;
using SmartHome.Shared.Contracts;
using SmartHome.Shared.Entities;
using SmartHome.Shared.Persistence;

namespace SmartHome.Backend.Commands;

public class OnOffCommand : ICommandHome
{
    public async Task<GoogleHomeCommandResponse> Execute(GoogleHomeExecution execution, List<string> ids, List<MiniDTO> minis, CancellationToken cancellationToken=default)
    {
        var onOff = execution.Params.On ?? true;        
        var mini = minis.First();
        var data = (mini.Data != null 
            ? System.Text.Json.JsonSerializer.Deserialize<GoogleHomeState>(mini.Data)
            : null) ?? new GoogleHomeState();
        data.On = onOff;
        mini.Data = System.Text.Json.JsonSerializer.Serialize(data);
        var ret = new GoogleHomeCommandResponse
            {
                Ids = ids,
                Status = "SUCCESS",
                States = data 
            };
        return ret;
    }
}