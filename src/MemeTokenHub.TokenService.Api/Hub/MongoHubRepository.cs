using MemeTokenHub.TokenService.Api.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
namespace MemeTokenHub.TokenService.Api.Hub;

public sealed class MongoHubRepository : IHubRepository
{
    private readonly IMongoCollection<HubEntry> _entries;
    public MongoHubRepository(IMongoClient client, IOptions<MongoDbOptions> options)
    {
        _entries = client.GetDatabase(options.Value.DatabaseName).GetCollection<HubEntry>("HubEntries");
    }
    public async Task<IReadOnlyList<HubEntry>> ListAsync(string kind, string? ownerId, CancellationToken cancellationToken)
    {
        FilterDefinition<HubEntry> filter = Builders<HubEntry>.Filter.Eq(x => x.Kind, kind);
        if (ownerId is not null)
            filter &= Builders<HubEntry>.Filter.Eq(x => x.OwnerId, ownerId);
        return await _entries.Find(filter).SortByDescending(x => x.UpdatedAt).Limit(500).ToListAsync(cancellationToken);
    }
    public Task<HubEntry?> GetAsync(string id, CancellationToken cancellationToken) => _entries.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken)!;
    public Task CreateAsync(HubEntry entry, CancellationToken cancellationToken) => _entries.InsertOneAsync(entry, cancellationToken: cancellationToken);
    public async Task<bool> ReplaceAsync(HubEntry entry, long expectedVersion, CancellationToken cancellationToken)
    {
        ReplaceOneResult result = await _entries.ReplaceOneAsync(x => x.Id == entry.Id && x.Version == expectedVersion, entry, cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }
}
