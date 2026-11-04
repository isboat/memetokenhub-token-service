using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MemeTokenHub.TokenService.Api.Domain;

public sealed class TokenProject
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public required string TokenId { get; init; }

    public required string Name { get; set; }

    public required string Symbol { get; set; }

    public required string Description { get; set; }

    public required string Network { get; set; }

    public required string ContractAddress { get; set; }

    public required string Category { get; set; }

    public required string CreatorId { get; init; }

    public string? LogoUrl { get; set; }

    public string? BannerUrl { get; set; }

    public string? UpcomingProfileId { get; init; }

    public string? WebsiteUrl { get; set; }

    public IReadOnlyCollection<SocialLink> SocialLinks { get; set; } = [];

    [BsonRepresentation(BsonType.String)]
    public TokenStatus Status { get; set; } = TokenStatus.Unclaimed;

    [BsonRepresentation(BsonType.String)]
    public LaunchStatus LaunchStatus { get; set; } = LaunchStatus.Draft;

    public TokenMetadata Metadata { get; set; } = new();

    public CommunityProjection Community { get; set; } = new();

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? PublishedAt { get; set; }

    public bool IsActive { get; set; } = true;

    public long Version { get; set; } = 1;
}
