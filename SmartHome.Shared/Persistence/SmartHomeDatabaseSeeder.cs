using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartHome.Shared.Entities;

namespace SmartHome.Shared.Persistence;

public static class SmartHomeDatabaseSeeder
{
    public static async Task SeedAsync(
        SmartHomeDbContext context,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var username = configuration["InitialAdmin:Username"];
        var password = configuration["InitialAdmin:Password"];
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (await context.Usuarios.AnyAsync(cancellationToken))
        {
            return;
        }

        var user = new Usuario
        {
            Username = username.Trim(),
            AgentUserId = configuration["InitialAdmin:AgentUserId"]?.Trim()
                ?? "usr_master_pi_01"
        };
        user.PasswordHash = new PasswordHasher<Usuario>().HashPassword(user, password);

        context.Usuarios.Add(user);
        await context.SaveChangesAsync(cancellationToken);
    }
}
