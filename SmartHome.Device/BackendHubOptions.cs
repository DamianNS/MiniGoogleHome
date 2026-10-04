namespace SmartHome.Device;

public sealed class BackendHubOptions
{
    public const string SectionName = "Backend";

    /// <summary>URL absoluta del hub SignalR del Backend.</summary>
    public string HubUrl { get; set; } = "http://localhost:5000/hubs/device";

    /// <summary>Token Bearer opcional. Proveer por variable de entorno Backend__AccessToken.</summary>
    public string? AccessToken { get; set; }
}
