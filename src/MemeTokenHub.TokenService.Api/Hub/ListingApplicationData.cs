using System.ComponentModel.DataAnnotations;
namespace MemeTokenHub.TokenService.Api.Hub;

public sealed class ListingApplicationData
{
    [Required, RegularExpression("^(network|exchange|launchpad)$")] public string Category { get; init; } = "";
    [Required, StringLength(100)] public string Name { get; init; } = "";
    [Required, StringLength(1000)] public string WebsiteUrl { get; init; } = "";
    [Required, StringLength(4000)] public string Story { get; init; } = "";
    [Required, EmailAddress, StringLength(200)] public string ContactEmail { get; init; } = "";
}
