namespace SmartHome.Shared.Entities;

public sealed class Usuario
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string AgentUserId { get; set; } = string.Empty;
}
