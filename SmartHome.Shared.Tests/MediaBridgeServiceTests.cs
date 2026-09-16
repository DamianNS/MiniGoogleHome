using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using SmartHome.Backend.Services;
using SmartHome.Shared.Persistence;

namespace SmartHome.Shared.Tests;

public sealed class MediaBridgeServiceTests
{
    [Fact]
    public async Task Play_records_one_successful_history_entry_and_delegates_volume()
    {
        await using var connection = new SqliteConnection("Data Source=file:media-bridge-tests?mode=memory&cache=shared");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SmartHomeDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var setupContext = new SmartHomeDbContext(options);
        await setupContext.Database.EnsureCreatedAsync();

        var audio = new FakeAudioPlayer { Result = new AudioOperationResult(true) };
        var volume = new FakeVolumeController { Current = 70 };
        var service = new MediaBridgeService(
            audio,
            volume,
            new PooledDbContextFactory<SmartHomeDbContext>(options),
            NullLogger<MediaBridgeService>.Instance);

        var play = await service.PlayAsync("Shakira");
        var setVolume = await service.SetVolumeAsync(70);
        var currentVolume = await service.GetCurrentVolumeAsync();
        var history = await setupContext.HistorialReproducciones.SingleAsync();

        Assert.True(play.Succeeded);
        Assert.Equal("Shakira", audio.LastQuery);
        Assert.True(setVolume.Succeeded);
        Assert.Equal(70, volume.LastSet);
        Assert.Equal(70, currentVolume);
        Assert.Equal("Shakira", history.QueryTexto);
        Assert.True(history.Exitoso);
    }

    private sealed class FakeAudioPlayer : IAudioPlayer
    {
        public string? LastQuery { get; private set; }

        public AudioOperationResult Result { get; set; } = new(false);

        public Task<AudioOperationResult> PlayAsync(string query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeVolumeController : IVolumeController
    {
        public int? LastSet { get; private set; }

        public int? Current { get; set; }

        public Task<AudioOperationResult> SetAsync(int volumeLevel, CancellationToken cancellationToken = default)
        {
            LastSet = volumeLevel;
            return Task.FromResult(new AudioOperationResult(true));
        }

        public Task<int?> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Current);
        }
    }
}
