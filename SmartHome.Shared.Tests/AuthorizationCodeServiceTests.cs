using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Options;
using SmartHome.Frontend.Services;
using SmartHome.Shared.Configuration;
using SmartHome.Shared.Persistence;

namespace SmartHome.Shared.Tests;

public sealed class AuthorizationCodeServiceTests
{
    [Fact]
    public async Task CreateAsync_persists_single_use_code_for_agent_user()
    {
        await using var connection = new SqliteConnection("Data Source=file:oauth-code-tests?mode=memory&cache=shared");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SmartHomeDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var setupContext = new SmartHomeDbContext(options);
        await setupContext.Database.EnsureCreatedAsync();

        var factory = new PooledDbContextFactory<SmartHomeDbContext>(options);
        var service = new AuthorizationCodeService(
            factory,
            Options.Create(new OAuthOptions { AuthorizationCodeLifetimeSeconds = 60 }));

        var code = await service.CreateAsync("usr_master_pi_01");
        var stored = await setupContext.OauthCodes.SingleAsync();

        Assert.False(string.IsNullOrWhiteSpace(code));
        Assert.Equal(code, stored.Code);
        Assert.Equal("usr_master_pi_01", stored.AgentUserId);
        Assert.False(stored.IsUsed);
        Assert.InRange(stored.ExpiresAt, DateTime.UtcNow.AddSeconds(45), DateTime.UtcNow.AddSeconds(61));
    }
}
