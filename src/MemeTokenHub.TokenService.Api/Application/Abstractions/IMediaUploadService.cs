using MemeTokenHub.TokenService.Api.Application.Models;

namespace MemeTokenHub.TokenService.Api.Application.Abstractions;

public interface IMediaUploadService
{
    MediaUploadResponse CreateUpload(MediaUploadRequest request, string creatorId);
}
