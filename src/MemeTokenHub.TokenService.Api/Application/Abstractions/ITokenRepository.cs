using MemeTokenHub.TokenService.Api.Application.Models;
using MemeTokenHub.TokenService.Api.Domain;

namespace MemeTokenHub.TokenService.Api.Application.Abstractions;

public interface ITokenRepository
{
    Task CreateAsync(TokenProject token, CancellationToken cancellationToken);

    Task<TokenProject?> GetByIdAsync(string tokenId, bool includeDrafts, CancellationToken cancellationToken);

    Task<PagedResult<TokenProject>> SearchAsync(TokenQuery query, bool includeDrafts, CancellationToken cancellationToken);

    Task UpdateAsync(TokenProject token, CancellationToken cancellationToken);

    Task<bool> ContractExistsAsync(string network, string contractAddress, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<NetworkResponse>> GetNetworksAsync(CancellationToken cancellationToken);
}
