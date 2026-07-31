using System.Security.Cryptography;
using System.Text;
using MemeTokenHub.TokenService.Api.Application.Abstractions;
using MemeTokenHub.TokenService.Api.Application.Models;
using MemeTokenHub.TokenService.Api.Configuration;
using Microsoft.Extensions.Options;

namespace MemeTokenHub.TokenService.Api.Infrastructure;

public sealed class MediaUploadService : IMediaUploadService
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
    };

    private readonly MediaStorageOptions _options;
    private readonly IClock _clock;

    public MediaUploadService(IOptions<MediaStorageOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public MediaUploadResponse CreateUpload(MediaUploadRequest request, string creatorId)
    {
        if (!AllowedContentTypes.Contains(request.ContentType))
        {
            throw new ApiException(StatusCodes.Status422UnprocessableEntity, "Unsupported media type", "Logos and banners must be JPEG, PNG, or WebP images.");
        }

        if (!request.AssetType.Equals("Logo", StringComparison.OrdinalIgnoreCase) && !request.AssetType.Equals("Banner", StringComparison.OrdinalIgnoreCase))
        {
            throw new ApiException(StatusCodes.Status422UnprocessableEntity, "Unsupported asset type", "Asset type must be Logo or Banner.");
        }

        string extension = Path.GetExtension(Path.GetFileName(request.FileName)).ToLowerInvariant();
        string assetKey = $"{creatorId}/{Guid.NewGuid():N}{extension}";
        DateTime expiresAt = _clock.UtcNow.AddMinutes(_options.UploadLifetimeMinutes);
        long expiresUnix = new DateTimeOffset(expiresAt).ToUnixTimeSeconds();
        string signaturePayload = $"{assetKey}:{expiresUnix}:{request.ContentType}";
        byte[] signatureBytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_options.SigningKey), Encoding.UTF8.GetBytes(signaturePayload));
        string signature = Convert.ToHexString(signatureBytes).ToLowerInvariant();

        return new MediaUploadResponse
        {
            UploadUrl = $"{_options.UploadBaseUrl.TrimEnd('/')}/{assetKey}?expires={expiresUnix}&signature={signature}",
            AssetUrl = $"{_options.AssetBaseUrl.TrimEnd('/')}/{assetKey}",
            ExpiresAt = expiresAt,
        };
    }
}
