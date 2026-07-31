using MemeTokenHub.TokenService.Api.Application.Abstractions;

namespace MemeTokenHub.TokenService.UnitTests;

internal sealed class FixedClock : IClock
{
    public DateTime UtcNow { get; init; }
}
