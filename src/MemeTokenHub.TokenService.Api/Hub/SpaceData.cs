using System.ComponentModel.DataAnnotations;
namespace MemeTokenHub.TokenService.Api.Hub;

public sealed class SpaceData
{
    [Required, StringLength(200)] public string Title { get; init; } = "";
    [Required, StringLength(200)] public string Subject { get; init; } = "";
    [Required, StringLength(100)] public string Host { get; init; } = "";
    public DateTimeOffset StartsAt { get; init; }
    [StringLength(1000)] public string EventUrl { get; init; } = "";
    [StringLength(1000)] public string RecordingUrl { get; init; } = "";
    [StringLength(6000)] public string Summary { get; init; } = "";
    public bool OwnerReviewed { get; init; }
}
