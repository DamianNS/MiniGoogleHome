using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SmartHome.Backend.Hubs;

/// <summary>
/// Hub SignalR al que se conecta la aplicación de consola SmartHome.Device.
/// El cliente recibe comandos mediante el método <c>ReceiveCommand</c>.
/// </summary>
[Authorize]
public sealed class DeviceHub(ILogger<DeviceHub> logger) : Hub
{
    public const string Route = "/hubs/device";
    public const string ReceiveCommandMethod = "ReceiveCommand";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, Context.UserIdentifier ?? "anonymous");
        logger.LogInformation("Dispositivo conectado: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation(exception, "Dispositivo desconectado: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
