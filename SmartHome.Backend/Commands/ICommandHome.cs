using SmartHome.Shared.Contracts;
using SmartHome.Shared.Entities;

namespace SmartHome.Backend.Commands;

internal interface ICommandHome
{
    Task<GoogleHomeCommandResponse> Execute(GoogleHomeExecution execution, List<string> ids, List<MiniDTO> minis, CancellationToken cancellationToken=default);

    public static GoogleHomeCommandResponse CreateCommandError(
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