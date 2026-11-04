namespace MemeTokenHub.TokenService.Api.Hub;

public sealed record HubRevision(long Version, DateTime At, string Stage, string Payload);
