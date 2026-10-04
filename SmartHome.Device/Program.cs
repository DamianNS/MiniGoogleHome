using SmartHome.Device;

if (args.Length > 0 && args[0] is "login" or "logout")
{
    var config = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .AddEnvironmentVariables()
        .Build();
    var backend = config.GetSection(BackendHubOptions.SectionName).Get<BackendHubOptions>() ?? new();
    var tokenStore = new FileTokenStore();

    return args[0] == "login"
        ? await AuthCommands.LoginAsync(backend, tokenStore)
        : AuthCommands.Logout(tokenStore);
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<BackendHubOptions>(builder.Configuration.GetSection(BackendHubOptions.SectionName));
builder.Services.AddSingleton<ITokenStore, FileTokenStore>();
builder.Services.AddHostedService<DeviceWorker>();
builder.Services.AddHostedService<KeyPressShutdownService>();

await builder.Build().RunAsync();
return 0;
