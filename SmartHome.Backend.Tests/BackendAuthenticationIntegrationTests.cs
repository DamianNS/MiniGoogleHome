using System.Net;
using System.Net.Http.Json;
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartHome.Shared.Contracts;
using SmartHome.Shared.Entities;
using SmartHome.Shared.Persistence;
using SmartHome.Backend.Services;

namespace SmartHome.Backend.Tests;

public sealed class BackendAuthenticationIntegrationTests
{
    [Fact]
    public async Task Webhook_requires_bearer_and_accepts_valid_local_token()
    {
        using var factory = new BackendWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var request = new GoogleHomeRequest
        {
            RequestId = "integration-sync",
            Inputs = [new GoogleHomeInput { Intent = GoogleHomeIntents.Sync }]
        };

        var anonymous = await client.PostAsJsonAsync("/api/smarthome", request);
        using var authenticatedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/smarthome")
        {
            Content = JsonContent.Create(request, options: GoogleHomeJson.Options)
        };
        authenticatedRequest.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "valid-access-token");
        var authenticated = await client.SendAsync(authenticatedRequest);
        using var expiredRequest = new HttpRequestMessage(HttpMethod.Post, "/api/smarthome")
        {
            Content = JsonContent.Create(request, options: GoogleHomeJson.Options)
        };
        expiredRequest.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "expired-access-token");
        var expired = await client.SendAsync(expiredRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, authenticated.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        var response = await authenticated.Content.ReadFromJsonAsync<GoogleHomeResponse>(GoogleHomeJson.Options);
        Assert.Equal("usr_master_pi_01", response!.Payload.AgentUserId);
    }

    [Fact]
    public async Task Webhook_handles_query_execute_and_persists_media_history()
    {
        using var factory = new BackendWebApplicationFactory();
        using var client = factory.CreateClient();

        var queryResponse = await SendAuthenticatedAsync(client, new GoogleHomeRequest
        {
            RequestId = "query-integration",
            Inputs =
            [new GoogleHomeInput
            {
                Intent = GoogleHomeIntents.Query,
                Payload = new GoogleHomeInputPayload
                {
                    Devices = [new GoogleHomeDeviceReference { Id = "pi_media_speaker_01" }]
                }
            }]
        });
        var query = await queryResponse.Content.ReadFromJsonAsync<GoogleHomeQueryResponse>(GoogleHomeJson.Options);

        var mediaResponse = await SendAuthenticatedAsync(client, new GoogleHomeRequest
        {
            RequestId = "media-integration",
            Inputs =
            [new GoogleHomeInput
            {
                Intent = GoogleHomeIntents.Execute,
                Payload = new GoogleHomeInputPayload
                {
                    Commands = [new GoogleHomeCommand
                    {
                        Devices = [new GoogleHomeDeviceReference { Id = "pi_media_speaker_01" }],
                        Execution = [new GoogleHomeExecution
                        {
                            Command = GoogleHomeCommands.MediaPlay,
                            Params = new GoogleHomeCommandParameters
                            {
                                MediaQuery = new GoogleHomeMediaQuery { Query = "Shakira" }
                            }
                        }]
                    }]
                }
            }]
        });
        var media = await mediaResponse.Content.ReadFromJsonAsync<GoogleHomeResponse>(GoogleHomeJson.Options);

        var volumeResponse = await SendAuthenticatedAsync(client, new GoogleHomeRequest
        {
            RequestId = "volume-integration",
            Inputs =
            [new GoogleHomeInput
            {
                Intent = GoogleHomeIntents.Execute,
                Payload = new GoogleHomeInputPayload
                {
                    Commands = [new GoogleHomeCommand
                    {
                        Devices = [new GoogleHomeDeviceReference { Id = "pi_media_speaker_01" }],
                        Execution = [new GoogleHomeExecution
                        {
                            Command = GoogleHomeCommands.SetVolume,
                            Params = new GoogleHomeCommandParameters { VolumeLevel = 70 }
                        }]
                    }]
                }
            }]
        });
        var volume = await volumeResponse.Content.ReadFromJsonAsync<GoogleHomeResponse>(GoogleHomeJson.Options);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SmartHomeDbContext>>().CreateDbContext();
        var historyCount = context.HistorialReproducciones.Count();

        Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);
        Assert.True(query!.Payload.Devices["pi_media_speaker_01"].Online);
        Assert.Equal(HttpStatusCode.OK, mediaResponse.StatusCode);
        Assert.Equal("PLAYING", media!.Payload.Commands.Single().States.PlaybackState);
        Assert.Equal(HttpStatusCode.OK, volumeResponse.StatusCode);
        Assert.Equal(70, volume!.Payload.Commands.Single().States.CurrentVolume);
        Assert.Equal(1, historyCount);
    }

    private static async Task<HttpResponseMessage> SendAuthenticatedAsync(
        HttpClient client,
        GoogleHomeRequest request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/smarthome")
        {
            Content = JsonContent.Create(request, options: GoogleHomeJson.Options)
        };
        message.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "valid-access-token");
        return await client.SendAsync(message);
    }

    private sealed class BackendWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string databasePath = Path.Combine(
            Path.GetTempPath(),
            $"smart-home-tests-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:SharedDatabase"] = $"Data Source={databasePath}",
                    ["OAuth:ClientId"] = "google-client",
                    ["OAuth:ClientSecret"] = "client-secret"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<SmartHomeDbContext>>();
                services.RemoveAll<IDbContextFactory<SmartHomeDbContext>>();
                services.AddDbContextFactory<SmartHomeDbContext>(options =>
                    options.UseSqlite($"Data Source={databasePath}"));
                services.RemoveAll<IAudioPlayer>();
                services.RemoveAll<IVolumeController>();
                services.AddSingleton<IAudioPlayer, FakeAudioPlayer>();
                services.AddSingleton<IVolumeController, FakeVolumeController>();

                using var provider = services.BuildServiceProvider();
                using var scope = provider.CreateScope();
                using var context = scope.ServiceProvider
                    .GetRequiredService<IDbContextFactory<SmartHomeDbContext>>()
                    .CreateDbContext();
                context.Database.EnsureCreated();
                context.OauthTokens.AddRange(
                    new OauthToken
                    {
                        AccessToken = "valid-access-token",
                        RefreshToken = "valid-refresh-token",
                        AgentUserId = "usr_master_pi_01",
                        AccessExpiresAt = DateTime.UtcNow.AddHours(1)
                    },
                    new OauthToken
                    {
                        AccessToken = "expired-access-token",
                        RefreshToken = "expired-refresh-token",
                        AgentUserId = "usr_master_pi_01",
                        AccessExpiresAt = DateTime.UtcNow.AddMinutes(-1)
                    });
                context.SaveChanges();
            });
        }

        private sealed class FakeAudioPlayer : IAudioPlayer
        {
            public Task<AudioOperationResult> PlayAsync(string query, CancellationToken cancellationToken = default) =>
                Task.FromResult(new AudioOperationResult(true));
        }

        private sealed class FakeVolumeController : IVolumeController
        {
            public Task<AudioOperationResult> SetAsync(int volumeLevel, CancellationToken cancellationToken = default) =>
                Task.FromResult(new AudioOperationResult(volumeLevel is >= 0 and <= 100));

            public Task<int?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult<int?>(70);
        }
    }
}
