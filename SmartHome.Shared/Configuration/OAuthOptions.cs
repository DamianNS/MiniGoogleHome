namespace SmartHome.Shared.Configuration;

public sealed class OAuthOptions
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string[] AllowedRedirectUris { get; set; } = [];

    public int AuthorizationCodeLifetimeSeconds { get; set; } = 300;

    public int AccessTokenLifetimeSeconds { get; set; } = 3600;
}
