using SmartHome.Device;

namespace SmartHome.Device;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.Configure<BackendHubOptions>(builder.Configuration.GetSection(BackendHubOptions.SectionName));
        builder.Services.AddHostedService<DeviceWorker>();
        builder.Services.AddHostedService<KeyPressShutdownService>();

        await builder.Build().RunAsync();
    }
}
