using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using SmartHome.Backend.Authentication;
using SmartHome.Backend.Configuration;
using SmartHome.Backend.Services;
using SmartHome.Shared.Configuration;
using SmartHome.Shared.Persistence;

namespace SmartHome.Backend;

public static class Program {

    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers();
        builder.Services.AddSmartHomePersistence(builder.Configuration);
        builder.Services.AddScoped<OAuthTokenService>();
        builder.Services.AddScoped<MediaBridgeService>();
        builder.Services.AddSingleton<IExternalProcessRunner, SystemExternalProcessRunner>();
        builder.Services.AddSingleton<IAudioPlayer, LinuxAudioPlayer>();
        builder.Services.AddSingleton<IVolumeController, LinuxVolumeController>();
        builder.Services.Configure<OAuthOptions>(builder.Configuration.GetSection("OAuth"));
        builder.Services.Configure<AudioOptions>(builder.Configuration.GetSection("Audio"));
        builder.Services
            .AddAuthentication("Bearer")
            .AddScheme<AuthenticationSchemeOptions, LocalBearerAuthenticationHandler>("Bearer", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddProblemDetails();
        builder.Services.AddCors(options =>
        {
            var allowedOrigins = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? [];

            options.AddPolicy("AdminFrontend", policy =>
            {
                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                }
            });
        });
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();

        await SmartHomeDatabaseBootstrapper.ApplyMigrationsAndSeedAsync(
            app.Services,
            app.Configuration);

        // Configure the HTTP request pipeline.
        app.UseExceptionHandler();
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();
        app.UseCors("AdminFrontend");

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/health", async (IDbContextFactory<SmartHomeDbContext> dbContextFactory, CancellationToken cancellationToken) =>
        {
            try
            {
                await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                var databaseAvailable = await context.Database.CanConnectAsync(cancellationToken);
                return databaseAvailable
                    ? Results.Ok(new { status = "ok" })
                    : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
            catch (Exception)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        });

        app.MapControllers();

        app.Run();
    }
}