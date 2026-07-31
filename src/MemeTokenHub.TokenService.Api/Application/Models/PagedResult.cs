namespace MemeTokenHub.TokenService.Api.Application.Models;

public sealed class PagedResult<T>
{
    public required IReadOnlyCollection<T> Items { get; init; }

    public required int Limit { get; init; }

    public required int Offset { get; init; }

    public required long Total { get; init; }
}
