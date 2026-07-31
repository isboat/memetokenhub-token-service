namespace MemeTokenHub.TokenService.Api.Application.Models;

public sealed class MediaUploadResponse
{
    public required string UploadUrl { get; init; }

    public required string AssetUrl { get; init; }

    public required DateTime ExpiresAt { get; init; }
}
