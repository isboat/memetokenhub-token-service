using System.ComponentModel.DataAnnotations;
namespace MemeTokenHub.TokenService.Api.Hub;

public sealed record UpcomingProfileData
{
    [Required, StringLength(100)] public string Name { get; init; } = "";
    [Required, StringLength(15)] public string Symbol { get; init; } = "";
    [Required, StringLength(50)] public string Network { get; init; } = "";
    [Required, StringLength(2000)] public string Story { get; init; } = "";
    [Required, StringLength(2000)] public string Distribution { get; init; } = "";
    [Required, StringLength(2000)] public string Disclosures { get; init; } = "";
    public DateTimeOffset LaunchAt { get; init; }
    [Required, StringLength(100)] public string Launchpad { get; init; } = "";
    [Required, StringLength(100)] public string LaunchType { get; init; } = "";
    [Required, StringLength(1000)] public string LogoUrl { get; init; } = "";
    [StringLength(1000)] public string WebsiteUrl { get; init; } = "";
    [StringLength(1000)] public string TelegramUrl { get; init; } = "";
    [StringLength(200)] public string ContractAddress { get; init; } = "";
    [StringLength(1000)] public string TradingUrl { get; init; } = "";
    public bool Sponsored { get; init; }

    public UpcomingProfileData NormalizeIdentity() => this with { Network = Network.Trim(), ContractAddress = ContractAddress.Trim() };
}
