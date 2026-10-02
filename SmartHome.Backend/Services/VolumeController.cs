using System.Globalization;
using Microsoft.Extensions.Options;
using SmartHome.Backend.Configuration;

namespace SmartHome.Backend.Services;

public interface IVolumeController
{
    Task<AudioOperationResult> SetAsync(int volumeLevel, CancellationToken cancellationToken = default);

    Task<int?> GetCurrentAsync(CancellationToken cancellationToken = default);
}

public sealed class LinuxVolumeController(
    IExternalProcessRunner processRunner,
    IOptions<AudioOptions> audioOptions,
    ILogger<LinuxVolumeController> logger) : IVolumeController
{
    public async Task<AudioOperationResult> SetAsync(
        int volumeLevel,
        CancellationToken cancellationToken = default)
    {
        if (volumeLevel is < 0 or > 100)
        {
            volumeLevel = Math.Clamp(volumeLevel, 0, 100);
        }

        try
        {
            using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Unspecified);
            await socket.ConnectAsync(new System.Net.Sockets.UnixDomainSocketEndPoint("/tmp/mpv-socket"), cancellationToken);
            
            var command = $"set volume {volumeLevel}\n";
            var bytes = System.Text.Encoding.UTF8.GetBytes(command);
            await socket.SendAsync(bytes, System.Net.Sockets.SocketFlags.None, cancellationToken);
            
            return new AudioOperationResult(true);
        }
        catch (Exception exception)
        {
            logger.LogWarning("No se pudo ajustar el volumen mediante socket: {Error}", exception.Message);
            return new AudioOperationResult(false, "No se pudo ajustar el volumen.");
        }
    }

    public async Task<int?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Unspecified);
            await socket.ConnectAsync(new System.Net.Sockets.UnixDomainSocketEndPoint("/tmp/mpv-socket"), cancellationToken);
            
            var command = "{ \"command\": [\"get_property\", \"volume\"] }\n";
            var bytes = System.Text.Encoding.UTF8.GetBytes(command);
            await socket.SendAsync(bytes, System.Net.Sockets.SocketFlags.None, cancellationToken);
            
            var buffer = new byte[1024];
            var received = await socket.ReceiveAsync(buffer, System.Net.Sockets.SocketFlags.None, cancellationToken);
            if (received == 0) return null;
            
            var response = System.Text.Encoding.UTF8.GetString(buffer, 0, received);
            
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                try
                {
                    var doc = System.Text.Json.JsonDocument.Parse(line);
                    if (doc.RootElement.TryGetProperty("data", out var dataElement) && dataElement.TryGetDouble(out var vol))
                    {
                        return (int)Math.Round(vol);
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // Ignore non-JSON lines or parse errors
                }
            }
            
            return null;
        }
        catch (Exception exception)
        {
            logger.LogWarning("No se pudo consultar el volumen mediante socket: {Error}", exception.Message);
            return null;
        }
    }
}
