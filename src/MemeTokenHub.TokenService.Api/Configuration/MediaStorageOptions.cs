namespace MemeTokenHub.TokenService.Api.Configuration;

public sealed class MediaStorageOptions
{
    public const string SectionName = "MediaStorage";

    public required string UploadBaseUrl { get; init; }

    public required string AssetBaseUrl { get; init; }

    public required string SigningKey { get; init; }

    public int UploadLifetimeMinutes { get; init; } = 10;
}
