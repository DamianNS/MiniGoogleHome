using System.Text.Json;
using SmartHome.Shared.Contracts;

namespace SmartHome.Shared.Tests;

public sealed class GoogleHomeContractTests
{
    [Fact]
    public void Sync_request_deserializes_with_request_id_and_intent()
    {
        const string json = """
            {
              "requestId": "4478392110293811",
              "inputs": [{ "intent": "action.devices.SYNC" }]
            }
            """;

        var request = JsonSerializer.Deserialize<GoogleHomeRequest>(json, GoogleHomeJson.Options);

        Assert.NotNull(request);
        Assert.Equal("4478392110293811", request.RequestId);
        Assert.Equal(GoogleHomeIntents.Sync, Assert.Single(request.Inputs).Intent);
    }

    [Fact]
    public void Execute_media_play_deserializes_query()
    {
        const string json = """
            {
              "requestId": "1198273645524312",
              "inputs": [{
                "intent": "action.devices.EXECUTE",
                "payload": {
                  "commands": [{
                    "devices": [{ "id": "pi_media_speaker_01" }],
                    "execution": [{
                      "command": "action.devices.commands.mediaPlay",
                      "params": { "mediaQuery": { "query": "Shakira" } }
                    }]
                  }]
                }
              }]
            }
            """;

        var request = JsonSerializer.Deserialize<GoogleHomeRequest>(json, GoogleHomeJson.Options);
        var execution = Assert.Single(Assert.Single(Assert.Single(request!.Inputs).Payload.Commands).Execution);

        Assert.Equal(GoogleHomeCommands.MediaPlay, execution.Command);
        Assert.Equal("Shakira", execution.Params.MediaQuery!.Query);
    }

    [Fact]
    public void Execute_volume_deserializes_level_and_serializes_camel_case()
    {
        const string json = """
            {
              "requestId": "9928374615243516",
              "inputs": [{
                "intent": "action.devices.EXECUTE",
                "payload": {
                  "commands": [{
                    "devices": [{ "id": "pi_media_speaker_01" }],
                    "execution": [{
                      "command": "action.devices.commands.setVolume",
                      "params": { "volumeLevel": 70 }
                    }]
                  }]
                }
              }]
            }
            """;

        var request = JsonSerializer.Deserialize<GoogleHomeRequest>(json, GoogleHomeJson.Options);
        var execution = Assert.Single(Assert.Single(Assert.Single(request!.Inputs).Payload.Commands).Execution);
        var response = new GoogleHomeResponse
        {
            RequestId = request.RequestId,
            Payload = new GoogleHomeResponsePayload
            {
                Commands =
                [
                    new GoogleHomeCommandResponse
                    {
                        Ids = ["pi_media_speaker_01"],
                        Status = "SUCCESS",
                        States = new GoogleHomeState { CurrentVolume = execution.Params.VolumeLevel }
                    }
                ]
            }
        };

        var serialized = JsonSerializer.Serialize(response, GoogleHomeJson.Options);

        Assert.Equal(70, execution.Params.VolumeLevel);
        Assert.Contains("\"requestId\":\"9928374615243516\"", serialized);
        Assert.Contains("\"currentVolume\":70", serialized);
        Assert.DoesNotContain("AgentUserId", serialized);
    }

    [Fact]
    public void Unknown_json_fields_are_ignored_without_failing_deserialization()
    {
        const string json = """
            {
              "requestId": "request-1",
              "unexpected": { "internal": true },
              "inputs": [{ "intent": "action.devices.SYNC", "extra": "ignored" }]
            }
            """;

        var request = JsonSerializer.Deserialize<GoogleHomeRequest>(json, GoogleHomeJson.Options);

        Assert.Equal("request-1", request!.RequestId);
        Assert.Equal(GoogleHomeIntents.Sync, Assert.Single(request.Inputs).Intent);
    }
}
