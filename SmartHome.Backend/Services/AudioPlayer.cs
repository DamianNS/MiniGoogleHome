using System.Diagnostics;
using Microsoft.Extensions.Options;
using SmartHome.Backend.Configuration;

namespace SmartHome.Backend.Services;

public interface IAudioPlayer
{
    Task<AudioOperationResult> PlayAsync(string query, CancellationToken cancellationToken = default);
}

public sealed record AudioOperationResult(bool Succeeded, string? Error = null);

public sealed class LinuxAudioPlayer(
    IExternalProcessRunner processRunner,
    IOptions<AudioOptions> audioOptions,
    ILogger<LinuxAudioPlayer> logger) : IAudioPlayer, IDisposable
{
    private readonly object processLock = new();
    private Process? activeProcess;

    public async Task<AudioOperationResult> PlayAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)
            || query.Length > audioOptions.Value.MaxQueryLength)
        {
            return new AudioOperationResult(false, "La consulta de audio no es válida.");
        }

        StopActiveProcess();
        try
        {
            var process = await processRunner.StartAsync(
                audioOptions.Value.MpvPath,
                ["--no-video", $"ytsearch:{query}"],
                cancellationToken);
            if (process is null)
            {
                return new AudioOperationResult(false, "No se pudo iniciar mpv.");
            }

            lock (processLock)
            {
                activeProcess = process;
            }

            return new AudioOperationResult(true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            logger.LogWarning("No se pudo iniciar el reproductor de audio: {Error}", exception.Message);
            return new AudioOperationResult(false, "No se pudo iniciar el reproductor de audio.");
        }
    }

    public void Dispose()
    {
        StopActiveProcess();
    }

    private void StopActiveProcess()
    {
        lock (processLock)
        {
            if (activeProcess is null)
            {
                return;
            }

            try
            {
                if (!activeProcess.HasExited)
                {
                    activeProcess.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // El proceso terminó entre la comprobación y la liberación.
            }
            finally
            {
                activeProcess.Dispose();
                activeProcess = null;
            }
        }
    }
}
