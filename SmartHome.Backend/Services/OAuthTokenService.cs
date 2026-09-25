using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartHome.Backend.Contracts;
using SmartHome.Shared.Configuration;
using SmartHome.Shared.Entities;
using SmartHome.Shared.Persistence;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SmartHome.Backend.Services;

public sealed class OAuthTokenService(
    IDbContextFactory<SmartHomeDbContext> dbContextFactory,
    IOptions<OAuthOptions> oauthOptions,
    ILogger<OAuthTokenService> log,
    IOptions<JwtOptions> jwtOptions)
{
    public async Task<OAuthTokenServiceResult> ExchangeAsync(
        OAuthTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidClient(request.ClientId, request.ClientSecret)
            || !IsAllowedRedirectUri(request.RedirectUri))
        {
            log.LogCritical("El cliente OAuth no es válido. ClientId: {ClientId} {RedirectUri}", request.ClientId, request.RedirectUri);
            log.LogCritical("ClientSecret: {ClientSecret}", request.ClientSecret);
            return OAuthTokenServiceResult.Invalid("invalid_client", "El cliente OAuth no es válido.");
        }

        if (!string.Equals(request.GrantType, "authorization_code", StringComparison.Ordinal))
        {
            log.LogCritical("El grant_type no está soportado. ClientId: {ClientId} {GrantType}", request.ClientId, request.GrantType);
            return OAuthTokenServiceResult.Invalid("unsupported_grant_type", "El grant_type no está soportado.");
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            log.LogCritical("El código no es válido. ClientId: {ClientId} {Code}", request.ClientId, request.Code);
            return OAuthTokenServiceResult.Invalid("invalid_grant", "El código no es válido.");
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var code = await context.OauthCodes
            .SingleOrDefaultAsync(
                item => item.Code == request.Code, cancellationToken);
        if (code is null || code.IsUsed) // || code.ExpiresAt <= DateTime.UtcNow)
        {
            log.LogCritical("El código no es válido. ClientId: {ClientId} {Code}", request.ClientId, request.Code);
            return OAuthTokenServiceResult.Invalid("invalid_grant", "El código no es válido.");
        }

        var ids = code.AgentUserId.Remove(0, "usr_master_pi_".Length);
        Usuario? user;
        if (int.TryParse(ids, out var userId))
        {
            user = await context.Usuarios.FindAsync(userId, cancellationToken);
            if (user is null)
            {
                log.LogCritical("RefreshAsync El usuario no existe. ClientId: {ClientId} {AgentUserId}", request.ClientId, code.AgentUserId);
                return OAuthTokenServiceResult.Invalid("invalid_grant", "El usuario no existe.");
            }
        }
        else
        {
            log.LogCritical("RefreshAsync El AgentUserId no es válido. ClientId: {ClientId} {AgentUserId}", request.ClientId, code.AgentUserId);
            return OAuthTokenServiceResult.Invalid("invalid_grant", "El AgentUserId no es válido.");
        }

        var token = CreateToken(code.AgentUserId, user);
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
            log.LogCritical("RefreshAsync El cliente OAuth no es válido. ClientId: {ClientId}", request.ClientId);
            log.LogCritical("RefreshAsync ClientSecret: {ClientSecret}", request.ClientSecret);
            return OAuthTokenServiceResult.Invalid("invalid_client", "El cliente OAuth no es válido.");
        }

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            log.LogCritical("RefreshAsync El refresh token NULL no es válido. ClientId: {ClientId} {RefreshToken}", request.ClientId, request.RefreshToken);
            return OAuthTokenServiceResult.Invalid("invalid_grant", "El refresh token no es válido.");
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var token = await context.OauthTokens
            .SingleOrDefaultAsync(item => item.RefreshToken == request.RefreshToken, cancellationToken);
        if (token is null)
        {
            log.LogCritical("RefreshAsync El refresh token no es válido. ClientId: {ClientId} {RefreshToken}", request.ClientId, request.RefreshToken);
            return OAuthTokenServiceResult.Invalid("invalid_grant", "El refresh token no es válido.");
        }

        var ids = token.AgentUserId.Remove(0, "usr_master_pi_".Length);
        Usuario? user;
        if(int.TryParse(ids, out var userId))
        {
            user = await context.Usuarios.FindAsync( userId, cancellationToken);
            if (user is null)
            {
                log.LogCritical("RefreshAsync El usuario no existe. ClientId: {ClientId} {AgentUserId}", request.ClientId, token.AgentUserId);
                return OAuthTokenServiceResult.Invalid("invalid_grant", "El usuario no existe.");
            }
        }
        else
        {
            log.LogCritical("RefreshAsync El AgentUserId no es válido. ClientId: {ClientId} {AgentUserId}", request.ClientId, token.AgentUserId);
            return OAuthTokenServiceResult.Invalid("invalid_grant", "El AgentUserId no es válido.");
        }        

        var rotated = CreateToken(token.AgentUserId, user);
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

        if(string.IsNullOrWhiteSpace(options.ClientId)) log.LogCritical("El ClientId de OAuth no está configurado.");
        if(string.IsNullOrWhiteSpace(options.ClientSecret)) log.LogCritical("El ClientSecret de OAuth no está configurado.");
        if(!string.Equals(clientId, options.ClientId, StringComparison.Ordinal)) log.LogCritical("El ClientId proporcionado no coincide con el configurado. Proporcionado: {ClientId}, Configurado: {ConfiguredClientId}", clientId, options.ClientId);
        if(providedSecret.Length != configuredSecret.Length) log.LogCritical("La longitud del ClientSecret proporcionado no coincide con la longitud del configurado. Proporcionado: {ProvidedLength}, Configurado: {ConfiguredLength}", providedSecret.Length, configuredSecret.Length);
        if(!CryptographicOperations.FixedTimeEquals(providedSecret, configuredSecret)) log.LogCritical("El ClientSecret proporcionado no coincide con el configurado.");

        return !string.IsNullOrWhiteSpace(options.ClientId)
            && !string.IsNullOrWhiteSpace(options.ClientSecret)
            && string.Equals(clientId, options.ClientId, StringComparison.Ordinal)
            && providedSecret.Length == configuredSecret.Length
            && CryptographicOperations.FixedTimeEquals(providedSecret, configuredSecret);
    }

    private bool IsAllowedRedirectUri(string redirectUri)
    {
        if(string.IsNullOrWhiteSpace(redirectUri))
        {
            log.LogCritical("El redirect_uri proporcionado es nulo o vacío.");
            return false;
        }
        if(oauthOptions.Value.AllowedRedirectUris is null || oauthOptions.Value.AllowedRedirectUris.Count() == 0)
        {
            log.LogCritical("No hay redirect_uris permitidos configurados.");
            return false;
        }
        if(!oauthOptions.Value.AllowedRedirectUris.Contains(redirectUri, StringComparer.Ordinal))
        {
            log.LogCritical("El redirect_uri proporcionado no está permitido. Proporcionado: {RedirectUri}, Permitidos: {AllowedRedirectUris}", redirectUri, string.Join(", ", oauthOptions.Value.AllowedRedirectUris));
            return false;
        }
        return oauthOptions.Value.AllowedRedirectUris.Contains(redirectUri, StringComparer.Ordinal);
    }

    private OauthToken CreateToken(string agentUserId, Usuario user)
    {
        var lifetime = Math.Clamp(oauthOptions.Value.AccessTokenLifetimeSeconds, 60, 86400);
        return new OauthToken
        {
            AccessToken = GenerateAccessToken(user),
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

    private string GenerateAccessToken(Usuario user)
    {
        var lifetime = Math.Clamp(oauthOptions.Value.AccessTokenLifetimeSeconds, 60, 86400);
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(jwtOptions.Value.SecretKey);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("sub", user.Id.ToString()) // El claim "sub" que usas en tu UsuariosController
            }),
            Expires = DateTime.UtcNow.AddMinutes(lifetime), // Corta duración
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
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
