using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartHome.Backend.Contracts;
using SmartHome.Shared.Configuration;
using SmartHome.Shared.Entities;
using SmartHome.Shared.Persistence;

namespace SmartHome.Backend.Services;

public sealed class OAuthTokenService(
    IDbContextFactory<SmartHomeDbContext> dbContextFactory,
    IOptions<OAuthOptions> oauthOptions)
{
    public async Task<OAuthTokenServiceResult> ExchangeAsync(
        OAuthTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidClient(request.ClientId, request.ClientSecret)
            || !IsAllowedRedirectUri(request.RedirectUri))
        {
            return OAuthTokenServiceResult.Invalid("invalid_client", "El cliente OAuth no es válido.");
        }

        if (!string.Equals(request.GrantType, "authorization_code", StringComparison.Ordinal))
        {
            return OAuthTokenServiceResult.Invalid("unsupported_grant_type", "El grant_type no está soportado.");
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return OAuthTokenServiceResult.Invalid("invalid_grant", "El código no es válido.");
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var code = await context.OauthCodes
            .SingleOrDefaultAsync(
                item => item.Code == request.Code, cancellationToken);
        if (code is null || code.IsUsed) // || code.ExpiresAt <= DateTime.UtcNow)
        {
            return OAuthTokenServiceResult.Invalid("invalid_grant", "El código no es válido.");
        }

        var token = CreateToken(code.AgentUserId);
        code.IsUsed = true;
        context.OauthTokens.Add(token);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return OAuthTokenServiceResult.Success(CreateResponse(token));
    }

    public async Task<OAuthTokenServiceResult> RefreshAsync(
        OAuthTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidClient(request.ClientId, request.ClientSecret))
        {
            return OAuthTokenServiceResult.Invalid("invalid_client", "El cliente OAuth no es válido.");
        }

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return OAuthTokenServiceResult.Invalid("invalid_grant", "El refresh token no es válido.");
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var token = await context.OauthTokens
            .SingleOrDefaultAsync(item => item.RefreshToken == request.RefreshToken, cancellationToken);
        if (token is null)
        {
            return OAuthTokenServiceResult.Invalid("invalid_grant", "El refresh token no es válido.");
        }

        var rotated = CreateToken(token.AgentUserId);
        token.AccessToken = rotated.AccessToken;
        token.RefreshToken = rotated.RefreshToken;
        token.AccessExpiresAt = rotated.AccessExpiresAt;
        await context.SaveChangesAsync(cancellationToken);

        return OAuthTokenServiceResult.Success(CreateResponse(token));
    }

    private bool IsValidClient(string clientId, string clientSecret)
    {
        var options = oauthOptions.Value;
        var providedSecret = System.Text.Encoding.UTF8.GetBytes(clientSecret);
        var configuredSecret = System.Text.Encoding.UTF8.GetBytes(options.ClientSecret);
        return !string.IsNullOrWhiteSpace(options.ClientId)
            && !string.IsNullOrWhiteSpace(options.ClientSecret)
            && string.Equals(clientId, options.ClientId, StringComparison.Ordinal)
            && providedSecret.Length == configuredSecret.Length
            && CryptographicOperations.FixedTimeEquals(providedSecret, configuredSecret);
    }

    private bool IsAllowedRedirectUri(string redirectUri)
    {
        return oauthOptions.Value.AllowedRedirectUris.Contains(redirectUri, StringComparer.Ordinal);
    }

    private OauthToken CreateToken(string agentUserId)
    {
        var lifetime = Math.Clamp(oauthOptions.Value.AccessTokenLifetimeSeconds, 60, 86400);
        return new OauthToken
        {
            AccessToken = GenerateTokenValue(),
            RefreshToken = GenerateTokenValue(),
            AgentUserId = agentUserId,
            AccessExpiresAt = DateTime.UtcNow.AddSeconds(lifetime)
        };
    }

    private OAuthTokenResponse CreateResponse(OauthToken token)
    {
        var expiresIn = Math.Max(0, (int)(token.AccessExpiresAt - DateTime.UtcNow).TotalSeconds);
        return new OAuthTokenResponse
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            ExpiresIn = expiresIn
        };
    }

    private static string GenerateTokenValue()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}

public sealed class OAuthTokenServiceResult
{
    private OAuthTokenServiceResult(OAuthTokenResponse? response, OAuthTokenError? error)
    {
        Response = response;
        Error = error;
    }

    public OAuthTokenResponse? Response { get; }

    public OAuthTokenError? Error { get; }

    public bool IsSuccess => Response is not null;

    public static OAuthTokenServiceResult Success(OAuthTokenResponse response) => new(response, null);

    public static OAuthTokenServiceResult Invalid(string error, string description) =>
        new(null, new OAuthTokenError { Error = error, ErrorDescription = description });
}
