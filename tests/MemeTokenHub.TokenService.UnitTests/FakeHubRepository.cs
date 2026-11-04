using System.Text.Json;
using MemeTokenHub.TokenService.Api.Hub;
namespace MemeTokenHub.TokenService.UnitTests;

internal sealed class FakeHubRepository : IHubRepository
{
    private readonly Dictionary<string, HubEntry> _entries = [];
    private static HubEntry Clone(HubEntry entry) => JsonSerializer.Deserialize<HubEntry>(JsonSerializer.Serialize(entry))!;
    public Task CreateAsync(HubEntry entry, CancellationToken cancellationToken) { _entries.Add(entry.Id, Clone(entry)); return Task.CompletedTask; }
    public Task<HubEntry?> GetAsync(string id, CancellationToken cancellationToken) => Task.FromResult(_entries.TryGetValue(id, out HubEntry? entry) ? Clone(entry) : null);
    public Task<IReadOnlyList<HubEntry>> ListAsync(string kind, string? ownerId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<HubEntry>>(_entries.Values.Where(x => x.Kind == kind && (ownerId is null || x.OwnerId == ownerId)).Select(Clone).ToList());
    public Task<bool> ReplaceAsync(HubEntry entry, long expectedVersion, CancellationToken cancellationToken)
    {
        if (!_entries.TryGetValue(entry.Id, out HubEntry? current) || current.Version != expectedVersion)
            return Task.FromResult(false);
        _entries[entry.Id] = Clone(entry);
        return Task.FromResult(true);
    }
}
