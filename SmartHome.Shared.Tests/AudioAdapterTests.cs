using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartHome.Backend.Configuration;
using SmartHome.Backend.Services;

namespace SmartHome.Shared.Tests;

public sealed class AudioAdapterTests
{
    [Fact]
    public async Task AudioPlayer_uses_safe_mpv_arguments_without_shell()
    {
        var runner = new FakeProcessRunner();
        using var player = new LinuxAudioPlayer(
            runner,
            Options.Create(new AudioOptions { MpvPath = "mpv" }),
            NullLogger<LinuxAudioPlayer>.Instance);

        var result = await player.PlayAsync("Shakira & Jazz");

        Assert.True(result.Succeeded);
        Assert.Equal("mpv", runner.LastExecutable);
        Assert.Equal(["--no-video", "ytsearch:Shakira & Jazz"], runner.LastArguments);
    }

    [Fact]
    public async Task VolumeController_validates_range_and_builds_amixer_arguments()
    {
        var runner = new FakeProcessRunner
        {
            RunResult = new ExternalProcessResult(0, "Front Left: Playback 70%", string.Empty)
        };
        var controller = new LinuxVolumeController(
            runner,
            Options.Create(new AudioOptions { AmixerPath = "amixer", MixerName = "Master" }),
            NullLogger<LinuxVolumeController>.Instance);

        var invalid = await controller.SetAsync(101);
        var valid = await controller.SetAsync(70);

        Assert.False(invalid.Succeeded);
        Assert.True(valid.Succeeded);
        Assert.Equal("amixer", runner.LastExecutable);
        Assert.Equal(["set", "Master", "70%"], runner.LastArguments);
    }

    [Fact]
    public async Task VolumeController_parses_current_volume_and_returns_null_on_process_error()
    {
        var runner = new FakeProcessRunner
        {
            RunResult = new ExternalProcessResult(0, "Front Left: Playback 70%", string.Empty)
        };
        var controller = new LinuxVolumeController(
            runner,
            Options.Create(new AudioOptions()),
            NullLogger<LinuxVolumeController>.Instance);

        Assert.Equal(70, await controller.GetCurrentAsync());

        runner.RunResult = new ExternalProcessResult(1, string.Empty, "error");
        Assert.Null(await controller.GetCurrentAsync());
    }

    private sealed class FakeProcessRunner : IExternalProcessRunner
    {
        public string? LastExecutable { get; private set; }

        public IReadOnlyList<string> LastArguments { get; private set; } = [];

        public ExternalProcessResult RunResult { get; set; } = new(0, string.Empty, string.Empty);

        public Task<Process?> StartAsync(
            string executable,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken = default)
        {
            LastExecutable = executable;
            LastArguments = arguments.ToArray();
            return Task.FromResult<Process?>(Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit 0",
                UseShellExecute = false,
                CreateNoWindow = true
            }));
        }

        public Task<ExternalProcessResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken = default)
        {
            LastExecutable = executable;
            LastArguments = arguments.ToArray();
            return Task.FromResult(RunResult);
        }
    }
}
