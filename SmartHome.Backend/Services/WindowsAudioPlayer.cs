using SmartHome.Backend.Configuration;
using System.Diagnostics;
using LibVLCSharp.Shared;

namespace SmartHome.Backend.Services;

public class WindowsAudioPlayer(ILogger<LinuxAudioPlayer> logger) : IAudioPlayer, IDisposable
{
    private readonly object processLock = new();

    private const string urlRadio = "https://playerservices.streamtheworld.com/api/livestream-redirect/UNOAAC.aac";
    private LibVLC libVLC = new LibVLC();
    private MediaPlayer mediaPlayer { get; set; }

    private Media media { get; set; }

    public async Task<AudioOperationResult> StopAsync(
        CancellationToken cancellationToken = default)
    {
        StopActiveProcess();
        return new AudioOperationResult(true);
    }

    public async Task<AudioOperationResult> PlayAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        //if (string.IsNullOrWhiteSpace(query)
        //    || query.Length > audioOptions.Value.MaxQueryLength)
        //{
        //    return new AudioOperationResult(false, "La consulta de audio no es válida.");
        //}

        StopActiveProcess();
        try
        {
            Core.Initialize();

            libVLC = new LibVLC();
            mediaPlayer = new MediaPlayer(libVLC);
            media = new Media(libVLC, new Uri(urlRadio));
            mediaPlayer.Play(media);

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
            if (mediaPlayer is null)
            {
                return;
            }

            try
            {
               mediaPlayer.Stop();
            }
            catch (InvalidOperationException)
            {
                // El proceso terminó entre la comprobación y la liberación.
            }
            finally
            {
                mediaPlayer.Dispose();
            }
        }
    }
}
