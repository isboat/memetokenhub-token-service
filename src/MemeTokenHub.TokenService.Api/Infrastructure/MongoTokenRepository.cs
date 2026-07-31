using MemeTokenHub.TokenService.Api.Application.Abstractions;
using MemeTokenHub.TokenService.Api.Application.Models;
using MemeTokenHub.TokenService.Api.Configuration;
using MemeTokenHub.TokenService.Api.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace MemeTokenHub.TokenService.Api.Infrastructure;

public sealed class MongoTokenRepository : ITokenRepository
{
    private readonly IMongoCollection<TokenProject> _tokens;

    public MongoTokenRepository(IMongoClient mongoClient, IOptions<MongoDbOptions> options)
    {
        IMongoDatabase database = mongoClient.GetDatabase(options.Value.DatabaseName);
        _tokens = database.GetCollection<TokenProject>("Tokens");
    }

    public Task CreateAsync(TokenProject token, CancellationToken cancellationToken) =>
        _tokens.InsertOneAsync(token, cancellationToken: cancellationToken);

    public async Task<TokenProject?> GetByIdAsync(string tokenId, bool includeDrafts, CancellationToken cancellationToken)
    {
        FilterDefinition<TokenProject> filter = Builders<TokenProject>.Filter.Eq(token => token.TokenId, tokenId);
        if (!includeDrafts)
        {
            filter &= Builders<TokenProject>.Filter.Eq(token => token.LaunchStatus, LaunchStatus.Published);
        }

        return await _tokens.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedResult<TokenProject>> SearchAsync(TokenQuery query, bool includeDrafts, CancellationToken cancellationToken)
    {
        FilterDefinitionBuilder<TokenProject> filters = Builders<TokenProject>.Filter;
        FilterDefinition<TokenProject> filter = filters.Eq(token => token.IsActive, true);
        if (!includeDrafts)
        {
            filter &= filters.Eq(token => token.LaunchStatus, LaunchStatus.Published);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            filter &= filters.Text(query.Search);
        }

        if (!string.IsNullOrWhiteSpace(query.Network))
        {
            filter &= filters.Eq(token => token.Network, query.Network);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            filter &= filters.Eq(token => token.Category, query.Category);
        }

        if (!string.IsNullOrWhiteSpace(query.CreatorId))
        {
            filter &= filters.Eq(token => token.CreatorId, query.CreatorId);
        }

        if (query.Status.HasValue)
        {
            filter &= filters.Eq(token => token.Status, query.Status.Value);
        }

        SortDefinition<TokenProject> sort = query.SortBy.Equals("popularity", StringComparison.OrdinalIgnoreCase)
            ? Builders<TokenProject>.Sort.Descending(token => token.Community.TrendingScore).Descending(token => token.PublishedAt)
            : Builders<TokenProject>.Sort.Descending(token => token.PublishedAt).Descending(token => token.CreatedAt);

        long total = await _tokens.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        List<TokenProject> items = await _tokens.Find(filter)
            .Sort(sort)
            .Skip(query.Offset)
            .Limit(query.Limit)
            .ToListAsync(cancellationToken);

        return new PagedResult<TokenProject> { Items = items, Limit = query.Limit, Offset = query.Offset, Total = total };
    }

    public Task UpdateAsync(TokenProject token, CancellationToken cancellationToken) =>
        _tokens.ReplaceOneAsync(existing => existing.TokenId == token.TokenId, token, cancellationToken: cancellationToken);

    public Task<bool> ContractExistsAsync(string network, string contractAddress, CancellationToken cancellationToken) =>
        _tokens.Find(token => token.Network == network && token.ContractAddress == contractAddress).AnyAsync(cancellationToken);

    public async Task<IReadOnlyCollection<NetworkResponse>> GetNetworksAsync(CancellationToken cancellationToken)
    {
        List<NetworkResponse> networks = await _tokens.Aggregate()
            .Match(token => token.IsActive && token.LaunchStatus == LaunchStatus.Published)
            .Group(token => token.Network, group => new NetworkResponse { Network = group.Key, TokenCount = group.LongCount() })
            .SortBy(network => network.Network)
            .ToListAsync(cancellationToken);
        return networks;
    }
}
