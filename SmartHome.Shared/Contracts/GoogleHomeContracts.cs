using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartHome.Shared.Contracts;

public static class GoogleHomeIntents
{
    public const string Sync = "action.devices.SYNC";
    public const string Query = "action.devices.QUERY";
    public const string Execute = "action.devices.EXECUTE";
}

public static class GoogleHomeCommands
{
    public const string MediaPlay = "action.devices.commands.mediaPlay";
    public const string SetVolume = "action.devices.commands.setVolume";
}

public sealed class GoogleHomeRequest
{
    public string RequestId { get; set; } = string.Empty;

    public List<GoogleHomeInput> Inputs { get; set; } = [];
}

public sealed class GoogleHomeInput
{
    public string Intent { get; set; } = string.Empty;

    public GoogleHomeInputPayload Payload { get; set; } = new();
}

public sealed class GoogleHomeInputPayload
{
    public List<GoogleHomeDeviceReference> Devices { get; set; } = [];

    public List<GoogleHomeCommand> Commands { get; set; } = [];
}

public sealed class GoogleHomeDeviceReference
{
    public string Id { get; set; } = string.Empty;
}

public sealed class GoogleHomeCommand
{
    public List<GoogleHomeDeviceReference> Devices { get; set; } = [];

    public List<GoogleHomeExecution> Execution { get; set; } = [];
}

public sealed class GoogleHomeExecution
{
    public string Command { get; set; } = string.Empty;

    public GoogleHomeCommandParameters Params { get; set; } = new();
}

public sealed class GoogleHomeCommandParameters
{
    public GoogleHomeMediaQuery? MediaQuery { get; set; }

    public int? VolumeLevel { get; set; }
}

public sealed class GoogleHomeMediaQuery
{
    public string Query { get; set; } = string.Empty;
}

public sealed class GoogleHomeResponse
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public GoogleHomeResponsePayload Payload { get; set; } = new();
}

public sealed class GoogleHomeResponsePayload
{
    [JsonPropertyName("agentUserId")]
    public string? AgentUserId { get; set; }

    [JsonPropertyName("devices")]
    public List<GoogleHomeDevice> Devices { get; set; } = [];

    //[JsonPropertyName("commands")]
    //public List<GoogleHomeCommandResponse>? Commands { get; set; } = null;
}

public sealed class GoogleHomeQueryResponse
{
    public string RequestId { get; set; } = string.Empty;

    public GoogleHomeQueryPayload Payload { get; set; } = new();
}

public sealed class GoogleHomeQueryPayload
{
    public Dictionary<string, GoogleHomeState> Devices { get; set; } = [];
}

public sealed class GoogleHomeDevice
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("traits")]
    public List<string> Traits { get; set; } = [];

    [JsonPropertyName("name")]
    public GoogleHomeDeviceName Name { get; set; } = new();

    [JsonPropertyName("willReportState")]
    public bool WillReportState { get; set; }

    [JsonPropertyName("deviceInfo")]
    public GoogleHomeDeviceInfo DeviceInfo { get; set; } = new();

    [JsonPropertyName("attributes")]
    public GoogleHomeDeviceAttributes Attributes { get; set; } = new();
}

public sealed class GoogleHomeDeviceName
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("defaultNames")]
    public List<string> DefaultNames { get; set; } = [];

    [JsonPropertyName("nicknames")]
    public List<string> Nicknames { get; set; } = [];
}

public sealed class GoogleHomeDeviceInfo
{
    [JsonPropertyName("manufacturer")]
    public string Manufacturer { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("hwVersion")]
    public string HwVersion { get; set; } = string.Empty;

    [JsonPropertyName("swVersion")]
    public string SwVersion { get; set; } = string.Empty;
}

public sealed class GoogleHomeCommandResponse
{
    [JsonPropertyName("ids")]
    public List<string> Ids { get; set; } = [];

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("states")]
    public GoogleHomeState States { get; set; } = new();
}

public sealed class GoogleHomeState
{
    [JsonPropertyName("playbackState")]
    public string? PlaybackState { get; set; }

    [JsonPropertyName("currentVolume")]
    public int? CurrentVolume { get; set; }

    [JsonPropertyName("online")]
    public bool? Online { get; set; }
}

public sealed class GoogleHomeErrorResponse
{
    public string RequestId { get; set; } = string.Empty;

    public GoogleHomeErrorPayload Payload { get; set; } = new();
}

public sealed class GoogleHomeErrorPayload
{
    public string ErrorCode { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;
}

public static class GoogleHomeJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
    };
}

public sealed class GoogleHomeDeviceAttributes
{
    [JsonPropertyName("supportPlaybackState")]
    public bool supportPlaybackState { get; set; } = true;

    [JsonPropertyName("supportActivityState")]
    public bool supportActivityState { get; set; } = true;

    [JsonPropertyName("volumeMaxLevel")]
    public int volumeMaxLevel { get; set; } = 100;

    [JsonPropertyName("volumeCanMuteAndUnmute")]
    public bool volumeCanMuteAndUnmute { get; set; } = true;

    [JsonPropertyName("transportControlSupportedCommands")]
    public List<string> transportControlSupportedCommands { get; set; } = [
        "NEXT",
        "PAUSE",
        "PREVIOUS",
        "STOP",
        "RESUME"
        ];
}
