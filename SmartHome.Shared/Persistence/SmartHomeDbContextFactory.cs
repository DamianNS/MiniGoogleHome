using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartHome.Shared.Persistence;

public sealed class SmartHomeDbContextFactory : IDesignTimeDbContextFactory<SmartHomeDbContext>
{
    public SmartHomeDbContext CreateDbContext(string[] args)
    {
        var databasePath = Path.GetFullPath(Path.Combine("..", "data", "smarthome.db"));
        var databaseDirectory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        var options = new DbContextOptionsBuilder<SmartHomeDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new SmartHomeDbContext(options);
    }
}
