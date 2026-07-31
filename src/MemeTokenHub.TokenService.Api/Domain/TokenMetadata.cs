namespace MemeTokenHub.TokenService.Api.Domain;

public sealed class TokenMetadata
{
    public decimal Supply { get; set; }

    public decimal Price { get; set; }

    public decimal MarketCap { get; set; }

    public long Holders { get; set; }
}
