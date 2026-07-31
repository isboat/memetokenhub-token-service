using System.ComponentModel.DataAnnotations;

namespace MemeTokenHub.TokenService.Api.Application.Models;

public sealed class MediaUploadRequest
{
    [Required, StringLength(200)]
    public required string FileName { get; init; }

    [Required]
    public required string ContentType { get; init; }

    [Required]
    public required string AssetType { get; init; }
}
