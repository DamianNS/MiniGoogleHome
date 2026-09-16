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
            return new AudioOperationResult(false, "El volumen debe estar entre 0 y 100.");
        }

        try
        {
            var result = await processRunner.RunAsync(
                audioOptions.Value.AmixerPath,
                ["set", audioOptions.Value.MixerName, $"{volumeLevel}%"],
                cancellationToken);
            return result.ExitCode == 0
                ? new AudioOperationResult(true)
                : new AudioOperationResult(false, "amixer devolvió un error.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            logger.LogWarning("No se pudo ajustar el volumen: {Error}", exception.Message);
            return new AudioOperationResult(false, "No se pudo ajustar el volumen.");
        }
    }

    public async Task<int?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await processRunner.RunAsync(
                audioOptions.Value.AmixerPath,
                ["get", audioOptions.Value.MixerName],
                cancellationToken);
            if (result.ExitCode != 0)
            {
                return null;
            }

            var marker = result.StandardOutput.IndexOf('%');
            if (marker <= 0)
            {
                return null;
            }

            var start = marker - 1;
            while (start >= 0 && char.IsDigit(result.StandardOutput[start]))
            {
                start--;
            }

            return int.TryParse(
                result.StandardOutput[(start + 1)..marker],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var volume)
                ? Math.Clamp(volume, 0, 100)
                : null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            logger.LogWarning("No se pudo consultar el volumen: {Error}", exception.Message);
            return null;
        }
    }
}
