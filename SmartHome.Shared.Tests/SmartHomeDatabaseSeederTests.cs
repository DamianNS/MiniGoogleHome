using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartHome.Shared.Persistence;

namespace SmartHome.Shared.Tests;

public sealed class SmartHomeDatabaseSeederTests
{
    [Fact]
    public async Task Seeder_creates_initial_user_once_when_credentials_are_configured()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SmartHomeDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new SmartHomeDbContext(options);
        await context.Database.MigrateAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InitialAdmin:Username"] = "admin",
                ["InitialAdmin:Password"] = "test-password",
                ["InitialAdmin:AgentUserId"] = "usr_master_pi_01"
            })
            .Build();

        await SmartHomeDatabaseSeeder.SeedAsync(context, configuration);
        await SmartHomeDatabaseSeeder.SeedAsync(context, configuration);

        var user = await context.Usuarios.SingleAsync();
        Assert.Equal("admin", user.Username);
        Assert.Equal("usr_master_pi_01", user.AgentUserId);
        Assert.NotEqual("test-password", user.PasswordHash);
    }
}
