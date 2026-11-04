using MongoDB.Bson.Serialization.Attributes;
namespace MemeTokenHub.TokenService.Api.Hub;

public sealed class SocialSnapshot
{
    [BsonId] public required string Id { get; init; }
    public string[] Links { get; init; } = [];
    public long Version { get; init; }
}
