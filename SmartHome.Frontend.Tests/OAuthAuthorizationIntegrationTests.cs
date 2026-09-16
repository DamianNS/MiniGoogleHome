using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartHome.Shared.Entities;
using SmartHome.Shared.Persistence;

namespace SmartHome.Frontend.Tests;

public sealed class OAuthAuthorizationIntegrationTests
{
    [Fact]
    public async Task Authorize_validates_credentials_state_and_redirect_allowlist()
    {
        using var factory = new FrontendWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var authorizeUrl = "/oauth/authorize?client_id=google-client"
            + "&redirect_uri=https%3A%2F%2Fexample.test%2Fcallback"
            + "&response_type=code&state=state-123";
        var page = await client.GetAsync(authorizeUrl);
        var html = await page.Content.ReadAsStringAsync();
        var antiforgery = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value;

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgery,
            ["client_id"] = "google-client",
            ["redirect_uri"] = "https://example.test/callback",
            ["response_type"] = "code",
            ["state"] = "state-123",
            ["username"] = "admin",
            ["password"] = "correct-password"
        });
        var response = await client.PostAsync("/oauth/authorize/submit", form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.Equal("https://example.test/callback", location!.GetLeftPart(UriPartial.Path));
        Assert.Contains("code=", location.Query);
        Assert.Contains("state=state-123", location.Query);
    }

    private sealed class FrontendWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string databasePath = Path.Combine(
            Path.GetTempPath(),
            $"smart-home-frontend-tests-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:SharedDatabase"] = $"Data Source={databasePath}",
                    ["OAuth:ClientId"] = "google-client",
                    ["OAuth:AllowedRedirectUris:0"] = "https://example.test/callback"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<SmartHomeDbContext>>();
                services.RemoveAll<IDbContextFactory<SmartHomeDbContext>>();
                services.AddDbContextFactory<SmartHomeDbContext>(options =>
                    options.UseSqlite($"Data Source={databasePath}"));

                using var provider = services.BuildServiceProvider();
                using var scope = provider.CreateScope();
                using var context = scope.ServiceProvider
                    .GetRequiredService<IDbContextFactory<SmartHomeDbContext>>()
                    .CreateDbContext();
                context.Database.EnsureCreated();
                var user = new Usuario
                {
                    Username = "admin",
                    AgentUserId = "usr_master_pi_01"
                };
                user.PasswordHash = new PasswordHasher<Usuario>()
                    .HashPassword(user, "correct-password");
                context.Usuarios.Add(user);
                context.SaveChanges();
            });
        }
    }
}
