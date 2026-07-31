using MemeTokenHub.TokenService.Api.Application.Models;

namespace MemeTokenHub.TokenService.Api.Application.Abstractions;

public interface ITokenService
{
    Task<TokenResponse> CreateAsync(CreateTokenRequest request, string creatorId, CancellationToken cancellationToken);

    Task<TokenResponse> GetAsync(string tokenId, string? requesterId, CancellationToken cancellationToken);

    Task<PagedResult<TokenResponse>> SearchAsync(TokenQuery query, CancellationToken cancellationToken);

    Task<TokenResponse> UpdateAsync(string tokenId, UpdateTokenRequest request, string requesterId, bool isModerator, CancellationToken cancellationToken);

    Task<TokenResponse> PublishAsync(string tokenId, string requesterId, bool isModerator, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<NetworkResponse>> GetNetworksAsync(CancellationToken cancellationToken);

    Task<SentimentResponse> GetSentimentAsync(string tokenId, CancellationToken cancellationToken);
}
