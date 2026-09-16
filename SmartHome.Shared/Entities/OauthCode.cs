namespace SmartHome.Shared.Entities;

public sealed class OauthCode
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string AgentUserId { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; }
}
