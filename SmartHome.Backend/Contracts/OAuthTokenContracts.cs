namespace SmartHome.Backend.Contracts;

public sealed class OAuthTokenRequest
{
    public string GrantType { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class OAuthTokenResponse
{
    public string AccessToken { get; init; } = string.Empty;

    public string TokenType { get; init; } = "Bearer";

    public string RefreshToken { get; init; } = string.Empty;

    public int ExpiresIn { get; init; }
}

public sealed class OAuthTokenError
{
    public string Error { get; init; } = string.Empty;

    public string ErrorDescription { get; init; } = string.Empty;
}
