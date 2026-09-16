using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartHome.Shared.Entities;
using SmartHome.Shared.Persistence;

namespace SmartHome.Frontend.Services;

public sealed class AdminAuthenticationService(IDbContextFactory<SmartHomeDbContext> dbContextFactory)
{
    private readonly PasswordHasher<Usuario> passwordHasher = new();

    public async Task<Usuario?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await context.Usuarios
            .SingleOrDefaultAsync(item => item.Username == username.Trim(), cancellationToken);
        if (user is null)
        {
            return null;
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result == PasswordVerificationResult.Failed ? null : user;
    }
}
