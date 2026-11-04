using MongoDB.Bson.Serialization.Attributes;
namespace MemeTokenHub.TokenService.Api.Hub;

public sealed class HubFollow
{
    [BsonId] public required string Id { get; init; }
    public required string UserId { get; init; }
    public required string ProjectId { get; init; }
}
