using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartHome.Shared.Persistence;

namespace SmartHome.Backend.Authentication;

public sealed class LocalBearerAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IDbContextFactory<SmartHomeDbContext> dbContextFactory)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
        {
            return AuthenticateResult.NoResult();
        }

        var header = authorizationHeader.ToString();
        const string prefix = "Bearer ";
        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail("El esquema de autenticación no es Bearer.");
        }

        var tokenValue = header[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(tokenValue))
        {
            return AuthenticateResult.Fail("El bearer token está vacío.");
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(Context.RequestAborted);
        var token = await context.OauthTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.AccessToken == tokenValue, Context.RequestAborted);
        if (token is null || token.AccessExpiresAt <= DateTime.UtcNow)
        {
            return AuthenticateResult.Fail("El bearer token no es válido.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, token.AgentUserId),
            new Claim("agent_user_id", token.AgentUserId)
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
