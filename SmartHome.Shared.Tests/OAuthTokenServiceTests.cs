using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Options;
using SmartHome.Backend.Contracts;
using SmartHome.Backend.Services;
using SmartHome.Shared.Configuration;
using SmartHome.Shared.Entities;
using SmartHome.Shared.Persistence;

namespace SmartHome.Shared.Tests;

public sealed class OAuthTokenServiceTests
{
    [Fact]
    public async Task Exchange_consumes_code_once_and_refresh_rotates_tokens()
    {
        await using var connection = new SqliteConnection("Data Source=file:token-tests?mode=memory&cache=shared");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SmartHomeDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var setupContext = new SmartHomeDbContext(options);
        await setupContext.Database.EnsureCreatedAsync();
        setupContext.OauthCodes.Add(new OauthCode
        {
            Code = "valid-code",
            AgentUserId = "usr_master_pi_01",
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        });
        await setupContext.SaveChangesAsync();

        var factory = new PooledDbContextFactory<SmartHomeDbContext>(options);
        var service = CreateService(factory);
        var request = new OAuthTokenRequest
        {
            GrantType = "authorization_code",
            Code = "valid-code",
            ClientId = "google-client",
            ClientSecret = "client-secret",
            RedirectUri = "https://example.test/callback"
        };

        var exchanged = await service.ExchangeAsync(request);
        var reused = await service.ExchangeAsync(request);
        var refresh = await service.RefreshAsync(new OAuthTokenRequest
        {
            GrantType = "refresh_token",
            ClientId = request.ClientId,
            ClientSecret = request.ClientSecret,
            RefreshToken = exchanged.Response!.RefreshToken
        });

        Assert.True(exchanged.IsSuccess);
        Assert.False(reused.IsSuccess);
        Assert.True(refresh.IsSuccess);
        Assert.NotEqual(exchanged.Response.RefreshToken, refresh.Response!.RefreshToken);
        Assert.NotEqual(exchanged.Response.AccessToken, refresh.Response.AccessToken);
    }

    [Fact]
    public async Task Exchange_rejects_expired_code_and_wrong_client_without_throwing()
    {
        await using var connection = new SqliteConnection("Data Source=file:token-error-tests?mode=memory&cache=shared");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SmartHomeDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var setupContext = new SmartHomeDbContext(options);
        await setupContext.Database.EnsureCreatedAsync();
        setupContext.OauthCodes.Add(new OauthCode
        {
            Code = "expired-code",
            AgentUserId = "usr_master_pi_01",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await setupContext.SaveChangesAsync();

        var service = CreateService(new PooledDbContextFactory<SmartHomeDbContext>(options));
        var expired = await service.ExchangeAsync(new OAuthTokenRequest
        {
            GrantType = "authorization_code",
            Code = "expired-code",
            ClientId = "wrong-client",
            ClientSecret = "x",
            RedirectUri = "https://example.test/callback"
        });

        Assert.False(expired.IsSuccess);
        Assert.Equal("invalid_client", expired.Error!.Error);
    }

    private static OAuthTokenService CreateService(IDbContextFactory<SmartHomeDbContext> factory)
    {
        return new OAuthTokenService(
            factory,
            Options.Create(new OAuthOptions
            {
                ClientId = "google-client",
                ClientSecret = "client-secret",
                AllowedRedirectUris = ["https://example.test/callback"],
                AccessTokenLifetimeSeconds = 3600
            }));
    }
}
