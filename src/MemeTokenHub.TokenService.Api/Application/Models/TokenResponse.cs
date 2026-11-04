using MemeTokenHub.TokenService.Api.Domain;

namespace MemeTokenHub.TokenService.Api.Application.Models;

public sealed class TokenResponse
{
    public required string TokenId { get; init; }

    public required string Name { get; init; }

    public required string Symbol { get; init; }

    public required string Description { get; init; }

    public required string Network { get; init; }

    public required string ContractAddress { get; init; }

    public required string Category { get; init; }

    public required string CreatorId { get; init; }

    public string? LogoUrl { get; init; }

    public string? BannerUrl { get; init; }

    public string? UpcomingProfileId { get; init; }

    public string? WebsiteUrl { get; init; }

    public required IReadOnlyCollection<SocialLink> SocialLinks { get; init; }

    public required TokenStatus Status { get; init; }

    public required LaunchStatus LaunchStatus { get; init; }

    public required TokenMetadata Metadata { get; init; }

    public required CommunityProjection Community { get; init; }

    public required DateTime CreatedAt { get; init; }

    public required DateTime UpdatedAt { get; init; }

    public DateTime? PublishedAt { get; init; }
}
