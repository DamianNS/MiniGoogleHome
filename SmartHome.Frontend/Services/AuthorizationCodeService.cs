using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartHome.Shared.Configuration;
using SmartHome.Shared.Entities;
using SmartHome.Shared.Persistence;

namespace SmartHome.Frontend.Services;

public sealed class AuthorizationCodeService(
    IDbContextFactory<SmartHomeDbContext> dbContextFactory,
    IOptions<OAuthOptions> oauthOptions)
{
    public async Task<string> CreateAsync(
        string agentUserId,
        CancellationToken cancellationToken = default)
    {
        var code = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        var lifetime = Math.Clamp(oauthOptions.Value.AuthorizationCodeLifetimeSeconds, 30, 600);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        context.OauthCodes.Add(new OauthCode
        {
            Code = code,
            AgentUserId = agentUserId,
            ExpiresAt = DateTime.UtcNow.AddSeconds(lifetime),
            IsUsed = false
        });
        await context.SaveChangesAsync(cancellationToken);
        return code;
    }
}
