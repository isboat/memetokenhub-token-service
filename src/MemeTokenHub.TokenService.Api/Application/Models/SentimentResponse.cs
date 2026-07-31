namespace MemeTokenHub.TokenService.Api.Application.Models;

public sealed class SentimentResponse
{
    public required string TokenId { get; init; }

    public required long HotVotes { get; init; }

    public required long NotVotes { get; init; }

    public required decimal SentimentScore { get; init; }

    public required DateTime UpdatedAt { get; init; }
}
