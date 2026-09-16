using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SmartHome.Shared.Persistence;

public static class SmartHomeDatabaseBootstrapper
{
    public static async Task ApplyMigrationsAndSeedAsync(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>("Database:ApplyMigrations"))
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SmartHomeDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
        await SmartHomeDatabaseSeeder.SeedAsync(context, configuration, cancellationToken);
    }
}
