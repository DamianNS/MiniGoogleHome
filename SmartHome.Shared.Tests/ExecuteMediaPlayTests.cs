using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using SmartHome.Backend.Controllers;
using SmartHome.Backend.Services;
using SmartHome.Shared.Contracts;
using SmartHome.Shared.Persistence;

namespace SmartHome.Shared.Tests;

public sealed class ExecuteMediaPlayTests
{
    [Fact]
    public async Task Execute_media_play_starts_query_records_history_and_returns_playing()
    {
        await using var connection = new SqliteConnection("Data Source=file:execute-media-tests?mode=memory&cache=shared");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SmartHomeDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var setupContext = new SmartHomeDbContext(options);
        await setupContext.Database.EnsureCreatedAsync();
        var audio = new FakeAudioPlayer();
        var bridge = new MediaBridgeService(
            audio,
            new FakeVolumeController(),
            new PooledDbContextFactory<SmartHomeDbContext>(options),
            NullLogger<MediaBridgeService>.Instance);
        var controller = new SmartHomeController(bridge)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Handle(new GoogleHomeRequest
        {
            RequestId = "1198273645524312",
            Inputs =
            [
                new GoogleHomeInput
                {
                    Intent = GoogleHomeIntents.Execute,
                    Payload = new GoogleHomeInputPayload
                    {
                        Commands =
                        [
                            new GoogleHomeCommand
                            {
                                Devices = [new GoogleHomeDeviceReference { Id = "pi_media_speaker_01" }],
                                Execution =
                                [
                                    new GoogleHomeExecution
                                    {
                                        Command = GoogleHomeCommands.MediaPlay,
                                        Params = new GoogleHomeCommandParameters
                                        {
                                            MediaQuery = new GoogleHomeMediaQuery { Query = "Shakira" }
                                        }
                                    }
                                ]
                            }
                        ]
                    }
                }
            ]
        }, CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result).Value as GoogleHomeResponse;
        var commandResponse = Assert.Single(response!.Payload.Commands);
        Assert.Equal("SUCCESS", commandResponse.Status);
        Assert.Equal("PLAYING", commandResponse.States.PlaybackState);
        Assert.Equal("Shakira", audio.LastQuery);
        Assert.Equal("Shakira", await setupContext.HistorialReproducciones.Select(item => item.QueryTexto).SingleAsync());
    }

    [Fact]
    public async Task Execute_set_volume_returns_current_volume_and_rejects_out_of_range()
    {
        var volume = new FakeVolumeController();
        var bridge = new MediaBridgeService(
            new FakeAudioPlayer(),
            volume,
            new PooledDbContextFactory<SmartHomeDbContext>(
                new DbContextOptionsBuilder<SmartHomeDbContext>()
                    .UseSqlite("Data Source=:memory:")
                    .Options),
            NullLogger<MediaBridgeService>.Instance);
        var controller = new SmartHomeController(bridge)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Handle(new GoogleHomeRequest
        {
            RequestId = "9928374615243516",
            Inputs =
            [
                new GoogleHomeInput
                {
                    Intent = GoogleHomeIntents.Execute,
                    Payload = new GoogleHomeInputPayload
                    {
                        Commands =
                        [
                            new GoogleHomeCommand
                            {
                                Devices = [new GoogleHomeDeviceReference { Id = "pi_media_speaker_01" }],
                                Execution =
                                [
                                    new GoogleHomeExecution
                                    {
                                        Command = GoogleHomeCommands.SetVolume,
                                        Params = new GoogleHomeCommandParameters { VolumeLevel = 70 }
                                    }
                                ]
                            }
                        ]
                    }
                }
            ]
        }, CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result).Value as GoogleHomeResponse;
        var commandResponse = Assert.Single(response!.Payload.Commands);
        Assert.Equal("SUCCESS", commandResponse.Status);
        Assert.Equal(70, commandResponse.States.CurrentVolume);
        Assert.Equal(70, volume.LastSet);
    }

    private sealed class FakeAudioPlayer : IAudioPlayer
    {
        public string? LastQuery { get; private set; }

        public Task<AudioOperationResult> PlayAsync(string query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(new AudioOperationResult(true));
        }
    }

    private sealed class FakeVolumeController : IVolumeController
    {
        public int? LastSet { get; private set; }

        public Task<AudioOperationResult> SetAsync(int volumeLevel, CancellationToken cancellationToken = default) =>
            SetInternalAsync(volumeLevel);

        public Task<int?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<int?>(null);

        private Task<AudioOperationResult> SetInternalAsync(int volumeLevel)
        {
            LastSet = volumeLevel;
            return Task.FromResult(new AudioOperationResult(volumeLevel is >= 0 and <= 100));
        }
    }
}
