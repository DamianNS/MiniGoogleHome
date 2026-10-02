using System.Diagnostics;
using Microsoft.Extensions.Options;
using SmartHome.Backend.Configuration;

namespace SmartHome.Backend.Services;

public interface IAudioPlayer
{
    Task<AudioOperationResult> PlayAsync(string query, CancellationToken cancellationToken = default);
    Task<AudioOperationResult> StopAsync(CancellationToken cancellationToken = default);
}

public sealed record AudioOperationResult(bool Succeeded, string? Error = null);


