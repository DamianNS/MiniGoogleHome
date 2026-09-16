namespace SmartHome.Shared.Entities;

public sealed class OauthToken
{
    public int Id { get; set; }

    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public string AgentUserId { get; set; } = string.Empty;

    public DateTime AccessExpiresAt { get; set; }
}
