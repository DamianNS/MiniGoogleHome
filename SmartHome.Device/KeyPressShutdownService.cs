namespace SmartHome.Device;

/// <summary>
/// Detiene el host cuando se presiona una tecla. Si la entrada está redirigida
/// (p. ej. systemd), no hace nada y el host se detiene con SIGTERM/Ctrl+C.
/// </summary>
public sealed class KeyPressShutdownService(
    IHostApplicationLifetime lifetime,
    ILogger<KeyPressShutdownService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (Console.IsInputRedirected)
        {
            return;
        }

        await Task.Yield();
        Console.WriteLine("Presione cualquier tecla para salir...");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    Console.ReadKey(intercept: true);
                    logger.LogInformation("Tecla presionada; deteniendo aplicación.");
                    lifetime.StopApplication();
                    return;
                }

                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
