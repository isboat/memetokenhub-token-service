using System.Text.Json;
namespace MemeTokenHub.TokenService.Api.Hub;

public sealed record HubWriteRequest(JsonElement Data, long Version = 0);
