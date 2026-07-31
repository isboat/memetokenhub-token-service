namespace MemeTokenHub.TokenService.Api.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}
