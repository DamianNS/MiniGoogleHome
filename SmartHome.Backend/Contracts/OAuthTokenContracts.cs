using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace SmartHome.Backend.Contracts;

public sealed class OAuthTokenRequest
{
    [FromForm(Name = "grant_type")]
    public string GrantType { get; set; } = string.Empty;

    [FromForm(Name = "code")]
    public string Code { get; set; } = string.Empty;

    [FromForm(Name = "client_id")]
    public string ClientId { get; set; } = string.Empty;

    [FromForm(Name = "client_secret")]
    public string ClientSecret { get; set; } = string.Empty;

    [FromForm(Name = "redirect_uri")]
    public string RedirectUri { get; set; } = string.Empty;

    [FromForm(Name = "refresh_token")]
    public string? RefreshToken { get; set; }
}

public sealed class OAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = "Bearer";

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; init; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }
}

public sealed class OAuthTokenError
{
    public string Error { get; init; } = string.Empty;

    public string ErrorDescription { get; init; } = string.Empty;
}
