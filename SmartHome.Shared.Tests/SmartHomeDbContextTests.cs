using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartHome.Shared.Entities;
using SmartHome.Shared.Persistence;

namespace SmartHome.Shared.Tests;

public sealed class SmartHomeDbContextTests
{
    [Fact]
    public async Task Sqlite_schema_persists_all_entities_and_enforces_unique_indexes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SmartHomeDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new SmartHomeDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.Usuarios.Add(new Usuario
        {
            Username = "admin",
            PasswordHash = "hash",
            AgentUserId = "usr_master_pi_01"
        });
        context.OauthCodes.Add(new OauthCode
        {
            Code = "code-1",
            AgentUserId = "usr_master_pi_01",
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        });
        context.OauthTokens.Add(new OauthToken
        {
            AccessToken = "access-1",
            RefreshToken = "refresh-1",
            AgentUserId = "usr_master_pi_01",
            AccessExpiresAt = DateTime.UtcNow.AddHours(1)
        });
        context.HistorialReproducciones.Add(new HistorialReproduccion
        {
            QueryTexto = "Shakira",
            Fecha = DateTime.UtcNow,
            Exitoso = true
        });

        await context.SaveChangesAsync();

        Assert.Equal(1, await context.Usuarios.CountAsync());
        Assert.Equal(1, await context.OauthCodes.CountAsync());
        Assert.Equal(1, await context.OauthTokens.CountAsync());
        Assert.Equal(1, await context.HistorialReproducciones.CountAsync());

        context.Usuarios.Add(new Usuario
        {
            Username = "admin",
            PasswordHash = "other-hash",
            AgentUserId = "usr_other"
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public void Model_contains_required_lengths_and_indexes()
    {
        var options = new DbContextOptionsBuilder<SmartHomeDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var context = new SmartHomeDbContext(options);
        var model = context.Model;

        Assert.Equal(100, model.FindEntityType(typeof(Usuario))!.FindProperty(nameof(Usuario.Username))!.GetMaxLength());
        Assert.Equal(500, model.FindEntityType(typeof(OauthToken))!.FindProperty(nameof(OauthToken.AccessToken))!.GetMaxLength());
        Assert.Contains(
            model.FindEntityType(typeof(Usuario))!.GetIndexes(),
            index => index.IsUnique && index.Properties.Single().Name == nameof(Usuario.Username));
        Assert.Contains(
            model.FindEntityType(typeof(OauthCode))!.GetIndexes(),
            index => index.IsUnique && index.Properties.Single().Name == nameof(OauthCode.Code));
    }
}
