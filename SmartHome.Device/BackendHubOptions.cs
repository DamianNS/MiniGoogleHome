namespace SmartHome.Device;

public sealed class BackendHubOptions
{
    public const string SectionName = "Backend";

    /// <summary>URL base del Backend (login y hub).</summary>
    public string BaseUrl { get; set; } = "http://localhost:5134";

    public Uri LoginUri => new(new Uri(BaseUrl.TrimEnd('/') + "/"), "oauth/login");

    public Uri HubUri => new(new Uri(BaseUrl.TrimEnd('/') + "/"), "hubs/device");
}
