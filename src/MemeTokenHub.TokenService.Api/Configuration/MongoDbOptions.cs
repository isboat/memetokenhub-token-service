namespace MemeTokenHub.TokenService.Api.Configuration;

public sealed class MongoDbOptions
{
    public const string SectionName = "MongoDb";

    public required string ConnectionString { get; init; }

    public string DatabaseName { get; init; } = "MemeTokenHubTokens";
}
