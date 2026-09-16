using Microsoft.EntityFrameworkCore;
using SmartHome.Shared.Entities;
using SmartHome.Shared.Persistence;

namespace SmartHome.Backend.Services;

public sealed class MediaBridgeService(
    IAudioPlayer audioPlayer,
    IVolumeController volumeController,
    IDbContextFactory<SmartHomeDbContext> dbContextFactory,
    ILogger<MediaBridgeService> logger)
{
    public async Task<MediaCommandResult> PlayAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await audioPlayer.PlayAsync(query, cancellationToken);
            await RecordHistoryAsync(query, result.Succeeded, cancellationToken);
            return new MediaCommandResult(result.Succeeded, result.Error);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "El ejecutor de audio produjo una excepción.");
            await RecordHistoryAsync(query, false, cancellationToken);
            return new MediaCommandResult(false, "No se pudo iniciar la reproducción.");
        }
    }

    public async Task<MediaCommandResult> SetVolumeAsync(
        int volumeLevel,
        CancellationToken cancellationToken = default)
    {
        var result = await volumeController.SetAsync(volumeLevel, cancellationToken);
        return new MediaCommandResult(result.Succeeded, result.Error);
    }

    public Task<int?> GetCurrentVolumeAsync(CancellationToken cancellationToken = default)
    {
        return volumeController.GetCurrentAsync(cancellationToken);
    }

    private async Task RecordHistoryAsync(
        string query,
        bool succeeded,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            context.HistorialReproducciones.Add(new HistorialReproduccion
            {
                QueryTexto = query,
                Fecha = DateTime.UtcNow,
                Exitoso = succeeded
            });
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "No se pudo registrar el historial de reproducción.");
            throw;
        }
    }
}

public sealed record MediaCommandResult(bool Succeeded, string? Error = null);
