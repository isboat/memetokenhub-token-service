using System.ComponentModel.DataAnnotations;
using MemeTokenHub.TokenService.Api.Domain;

namespace MemeTokenHub.TokenService.Api.Application.Models;

public sealed class UpdateTokenRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public required string Name { get; init; }

    [Required, StringLength(2000)]
    public required string Description { get; init; }

    [Required, StringLength(50)]
    public required string Category { get; init; }

    [Url]
    public string? LogoUrl { get; init; }

    [Url]
    public string? BannerUrl { get; init; }

    [Url]
    public string? WebsiteUrl { get; init; }

    public IReadOnlyCollection<SocialLink> SocialLinks { get; init; } = [];
}
