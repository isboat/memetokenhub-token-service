using MemeTokenHub.TokenService.Api.Configuration;
using MemeTokenHub.TokenService.Api.Domain;
using MemeTokenHub.TokenService.Api.Hub;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace MemeTokenHub.TokenService.Api.Infrastructure;

public sealed class MongoIndexInitializer : IHostedService
{
    private readonly IMongoCollection<TokenProject> _tokens;
    private readonly IMongoDatabase _database;

    public MongoIndexInitializer(IMongoClient client, IOptions<MongoDbOptions> options)
    {
        _database = client.GetDatabase(options.Value.DatabaseName);
        _tokens = _database.GetCollection<TokenProject>("Tokens");
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
        await _database.GetCollection<HubEntry>("HubEntries").Indexes.CreateManyAsync([
            new CreateIndexModel<HubEntry>(Builders<HubEntry>.IndexKeys.Ascending(x => x.Kind).Descending(x => x.UpdatedAt)),
            new CreateIndexModel<HubEntry>(Builders<HubEntry>.IndexKeys.Ascending(x => x.OwnerId).Ascending(x => x.Kind)),
        ], cancellationToken);
        await _database.GetCollection<SocialChange>("HubSocialChanges").Indexes.CreateOneAsync(
            new CreateIndexModel<SocialChange>(Builders<SocialChange>.IndexKeys.Descending(x => x.ObservedAt)), cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
