namespace MemeTokenHub.TokenService.Api.Hub;

public interface IHubRepository
{
    Task<IReadOnlyList<HubEntry>> ListAsync(string kind, string? ownerId, CancellationToken cancellationToken);
    Task<HubEntry?> GetAsync(string id, CancellationToken cancellationToken);
    Task CreateAsync(HubEntry entry, CancellationToken cancellationToken);
    Task<bool> ReplaceAsync(HubEntry entry, long expectedVersion, CancellationToken cancellationToken);
}
