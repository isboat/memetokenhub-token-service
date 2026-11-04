using MongoDB.Bson.Serialization.Attributes;
namespace MemeTokenHub.TokenService.Api.Hub;

public sealed class HubEntry
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public required string Kind { get; init; }
    public required string OwnerId { get; init; }
    public required string Payload { get; set; }
    public string Stage { get; set; } = "Upcoming";
    public string? TokenId { get; set; }
    public long Version { get; set; } = 1;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<HubRevision> History { get; set; } = [];
}
