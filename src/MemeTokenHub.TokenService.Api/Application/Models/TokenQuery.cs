using MemeTokenHub.TokenService.Api.Domain;

namespace MemeTokenHub.TokenService.Api.Application.Models;

public sealed class TokenQuery
{
    public string? Search { get; init; }

    public string? Network { get; init; }

    public string? Category { get; init; }

    public string? CreatorId { get; init; }

    public TokenStatus? Status { get; init; }

    public string SortBy { get; init; } = "createdAt";

    public int Limit { get; init; } = 20;

    public int Offset { get; init; }
}
