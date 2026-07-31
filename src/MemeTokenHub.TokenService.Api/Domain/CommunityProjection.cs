namespace MemeTokenHub.TokenService.Api.Domain;

public sealed class CommunityProjection
{
    public long FollowersCount { get; set; }

    public long HotVotes { get; set; }

    public long NotVotes { get; set; }

    public decimal SentimentScore { get; set; }

    public long SupportersCount { get; set; }

    public decimal TrendingScore { get; set; }

    public DateTime UpdatedAt { get; set; }
}
