using System.Diagnostics;

namespace SmartHome.Backend.Services;

public interface IExternalProcessRunner
{
    Task<Process?> StartAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);

    Task<ExternalProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}

public sealed record ExternalProcessResult(int ExitCode, string StandardOutput, string StandardError);

public sealed class SystemExternalProcessRunner : IExternalProcessRunner
{
    public Task<Process?> StartAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var startInfo = CreateStartInfo(executable, arguments, redirectOutput: false);
        return Task.FromResult(Process.Start(startInfo));
    }

    public async Task<ExternalProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var startInfo = CreateStartInfo(executable, arguments, redirectOutput: true);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"No se pudo iniciar {executable}.");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new ExternalProcessResult(
            process.ExitCode,
            await standardOutputTask,
            await standardErrorTask);
    }

    private static ProcessStartInfo CreateStartInfo(
        string executable,
        IReadOnlyList<string> arguments,
        bool redirectOutput)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = redirectOutput,
            RedirectStandardError = redirectOutput,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
