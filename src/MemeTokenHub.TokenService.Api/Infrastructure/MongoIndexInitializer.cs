using MemeTokenHub.TokenService.Api.Configuration;
using MemeTokenHub.TokenService.Api.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace MemeTokenHub.TokenService.Api.Infrastructure;

public sealed class MongoIndexInitializer : IHostedService
{
    private readonly IMongoCollection<TokenProject> _tokens;

    public MongoIndexInitializer(IMongoClient client, IOptions<MongoDbOptions> options)
    {
        _tokens = client.GetDatabase(options.Value.DatabaseName).GetCollection<TokenProject>("Tokens");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var indexes = new[]
        {
            new CreateIndexModel<TokenProject>(Builders<TokenProject>.IndexKeys.Ascending(token => token.TokenId), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<TokenProject>(Builders<TokenProject>.IndexKeys.Ascending(token => token.Network).Ascending(token => token.ContractAddress), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<TokenProject>(Builders<TokenProject>.IndexKeys.Text(token => token.Name).Text(token => token.Symbol).Text(token => token.Description)),
            new CreateIndexModel<TokenProject>(Builders<TokenProject>.IndexKeys.Ascending(token => token.LaunchStatus).Descending(token => token.PublishedAt)),
            new CreateIndexModel<TokenProject>(Builders<TokenProject>.IndexKeys.Descending(token => token.Community.TrendingScore)),
        };
        await _tokens.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
