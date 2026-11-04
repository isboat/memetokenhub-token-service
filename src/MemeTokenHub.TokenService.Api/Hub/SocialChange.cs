using MongoDB.Bson.Serialization.Attributes;
namespace MemeTokenHub.TokenService.Api.Hub;

public sealed class SocialChange
{
    [BsonId] public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Network { get; init; }
    public required string ContractAddress { get; init; }
    public required string Source { get; init; }
    public required string SourceUrl { get; init; }
    public DateTime ObservedAt { get; init; }
    public string[] PreviousLinks { get; init; } = [];
    public string[] CurrentLinks { get; init; } = [];
}
