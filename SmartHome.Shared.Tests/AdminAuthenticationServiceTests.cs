using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SmartHome.Frontend.Services;
using SmartHome.Shared.Entities;
using SmartHome.Shared.Persistence;

namespace SmartHome.Shared.Tests;

public sealed class AdminAuthenticationServiceTests
{
    [Fact]
    public async Task AuthenticateAsync_accepts_valid_credentials_and_rejects_invalid_ones()
    {
        await using var connection = new SqliteConnection("Data Source=file:auth-tests?mode=memory&cache=shared");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SmartHomeDbContext>()
            .UseSqlite(connection)
            .Options;
        await using (var setupContext = new SmartHomeDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
            var user = new Usuario
            {
                Username = "admin",
                AgentUserId = "usr_master_pi_01"
            };
            user.PasswordHash = new PasswordHasher<Usuario>().HashPassword(user, "correct-password");
            setupContext.Usuarios.Add(user);
            await setupContext.SaveChangesAsync();
        }

        var factory = new PooledDbContextFactory<SmartHomeDbContext>(options);
        var service = new AdminAuthenticationService(factory);

        var authenticated = await service.AuthenticateAsync("admin", "correct-password");
        var rejected = await service.AuthenticateAsync("admin", "wrong-password");
        var unknown = await service.AuthenticateAsync("missing", "correct-password");

        Assert.NotNull(authenticated);
        Assert.Null(rejected);
        Assert.Null(unknown);
        Assert.NotEqual("correct-password", authenticated.PasswordHash);
    }
}
