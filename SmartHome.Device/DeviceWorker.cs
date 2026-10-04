using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace SmartHome.Device;

/// <summary>
/// Mantiene la conexión SignalR con el Backend y queda a la espera de comandos.
/// </summary>
public sealed class DeviceWorker(
    IOptions<BackendHubOptions> options,
    ILogger<DeviceWorker> logger) : BackgroundService
{
    private static readonly TimeSpan[] ReconnectDelays =
    [
        TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)
    ];

    private readonly BackendHubOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var builder = new HubConnectionBuilder()
            .WithUrl(_options.HubUrl, o =>
            {
                if (!string.IsNullOrWhiteSpace(_options.AccessToken))
                {
                    o.AccessTokenProvider = () => Task.FromResult<string?>(_options.AccessToken);
                }
            })
            .WithAutomaticReconnect(new RetryPolicy());

        await using var connection = builder.Build();

        connection.On<string>("ReceiveCommand", command =>
            logger.LogInformation("Comando recibido ({Length} caracteres).", command?.Length ?? 0));

        connection.Reconnecting += ex =>
        {
            logger.LogWarning(ex, "Conexión perdida; reconectando...");
            return Task.CompletedTask;
        };
        connection.Reconnected += id =>
        {
            logger.LogInformation("Reconectado ({ConnectionId}).", id);
            return Task.CompletedTask;
        };
        connection.Closed += ex =>
        {
            if (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Conexión cerrada.");
            }
            return Task.CompletedTask;
        };

        await ConnectWithRetryAsync(connection, stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Apagado solicitado.
        }

        logger.LogInformation("Cerrando conexión con el Backend...");
        await connection.StopAsync(CancellationToken.None);
    }

    private async Task ConnectWithRetryAsync(HubConnection connection, CancellationToken ct)
    {
        var attempt = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await connection.StartAsync(ct);
                logger.LogInformation("Conectado a {Url}. Esperando comandos.", _options.HubUrl);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(30, 2 << Math.Min(attempt++, 4)));
                logger.LogWarning(ex, "No se pudo conectar; reintentando en {Delay}s.", delay.TotalSeconds);
                try { await Task.Delay(delay, ct); } catch (OperationCanceledException) { return; }
            }
        }
    }

    private sealed class RetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext context) =>
            context.PreviousRetryCount < ReconnectDelays.Length
                ? ReconnectDelays[context.PreviousRetryCount]
                : TimeSpan.FromSeconds(30);
    }
}
