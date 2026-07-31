namespace MemeTokenHub.TokenService.Api.Domain;

public sealed class SocialLink
{
    public required string Platform { get; init; }

    public required string Url { get; init; }
}
