using MemeTokenHub.TokenService.Api.Application.Abstractions;
using MemeTokenHub.TokenService.Api.Application.Models;
using MemeTokenHub.TokenService.Api.Domain;

namespace MemeTokenHub.TokenService.UnitTests;

internal sealed class FakeTokenRepository : ITokenRepository
{
    public List<TokenProject> Tokens { get; } = [];

    public Task CreateAsync(TokenProject token, CancellationToken cancellationToken)
    {
        Tokens.Add(token);
        return Task.CompletedTask;
    }

    public Task<TokenProject?> GetByIdAsync(string tokenId, bool includeDrafts, CancellationToken cancellationToken) =>
        Task.FromResult(Tokens.SingleOrDefault(token => token.TokenId == tokenId && (includeDrafts || token.LaunchStatus == LaunchStatus.Published)));

    public Task<PagedResult<TokenProject>> SearchAsync(TokenQuery query, bool includeDrafts, CancellationToken cancellationToken)
    {
        TokenProject[] items = Tokens.Where(token => includeDrafts || token.LaunchStatus == LaunchStatus.Published).ToArray();
        return Task.FromResult(new PagedResult<TokenProject> { Items = items, Limit = query.Limit, Offset = query.Offset, Total = items.Length });
    }

    public Task UpdateAsync(TokenProject token, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<bool> ContractExistsAsync(string network, string contractAddress, CancellationToken cancellationToken) =>
        Task.FromResult(Tokens.Any(token => token.Network == network && token.ContractAddress == contractAddress));

    public Task<IReadOnlyCollection<NetworkResponse>> GetNetworksAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<NetworkResponse>>([]);
}
