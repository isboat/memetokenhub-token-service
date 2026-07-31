using MemeTokenHub.TokenService.Api.Application.Abstractions;

namespace MemeTokenHub.TokenService.Api.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
