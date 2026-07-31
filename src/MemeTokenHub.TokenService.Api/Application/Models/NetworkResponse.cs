namespace MemeTokenHub.TokenService.Api.Application.Models;

public sealed class NetworkResponse
{
    public required string Network { get; init; }

    public required long TokenCount { get; init; }
}
