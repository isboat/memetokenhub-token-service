using System.Globalization;
using System.Text.Json;
using MemeTokenHub.TokenService.Api.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
namespace MemeTokenHub.TokenService.Api.Hub;

public sealed partial class SocialActivityCollector(IHttpClientFactory clients, IMongoClient mongo, IOptions<MongoDbOptions> options, IConfiguration configuration, ILogger<SocialActivityCollector> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("Hub:CollectDexScreener"))
            return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            { await CollectAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { CollectionFailed(logger, exception); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
    [LoggerMessage(Level = LogLevel.Warning, Message = "DEX Screener social snapshot collection failed; collection will retry at the next interval.")]
    private static partial void CollectionFailed(ILogger logger, Exception exception);

    public async Task CollectAsync(CancellationToken cancellationToken)
    {
        IMongoDatabase database = mongo.GetDatabase(options.Value.DatabaseName);
        IMongoCollection<SocialSnapshot> snapshots = database.GetCollection<SocialSnapshot>("HubSocialSnapshots");
        IMongoCollection<SocialChange> changes = database.GetCollection<SocialChange>("HubSocialChanges");
        // Recover before fetching: a pending token may no longer appear in the latest feed,
        // and a source outage must not prevent delivery of already-durable observations.
        using (IAsyncCursor<SocialSnapshot> pending = await snapshots.Find(x => x.PendingChange != null).ToCursorAsync(cancellationToken))
        {
            while (await pending.MoveNextAsync(cancellationToken))
                foreach (SocialSnapshot snapshot in pending.Current)
                    await FlushPendingAsync(snapshots, changes, snapshot, cancellationToken);
        }
        using HttpClient client = clients.CreateClient("HubSources");
        using HttpResponseMessage response = await client.GetAsync("https://api.dexscreener.com/token-profiles/latest/v1", cancellationToken);
        response.EnsureSuccessStatusCode();
        using JsonDocument document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        foreach (JsonElement row in document.RootElement.EnumerateArray().Take(100))
        {
            string address = row.GetProperty("tokenAddress").GetString() ?? "";
            string network = row.GetProperty("chainId").GetString() ?? "";
            if (address.Length == 0 || network.Length == 0)
                continue;
            string[] links = row.TryGetProperty("links", out JsonElement suppliedLinks)
                ? suppliedLinks.EnumerateArray().Where(x => x.TryGetProperty("type", out _)).Select(x => x.GetProperty("url").GetString() ?? "").Where(HubValidation.IsPublicHttpsUrl).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
                : [];
            await StoreObservationAsync(snapshots, changes, network, address, links, cancellationToken);
        }
    }

    private static async Task StoreObservationAsync(IMongoCollection<SocialSnapshot> snapshots, IMongoCollection<SocialChange> changes,
        string network, string address, string[] links, CancellationToken cancellationToken)
    {
        string key = $"dexscreener:{network}:{address}";
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SocialSnapshot? previous = await snapshots.Find(x => x.Id == key).FirstOrDefaultAsync(cancellationToken);
            if (previous is null)
            {
                try
                {
                    await snapshots.InsertOneAsync(new SocialSnapshot { Id = key, Links = links, Version = 1 }, cancellationToken: cancellationToken);
                    return;
                }
                catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
                {
                    // Another collector established the baseline. Compare with that snapshot.
                    continue;
                }
            }
            if (previous.PendingChange is not null)
            {
                await FlushPendingAsync(snapshots, changes, previous, cancellationToken);
                continue;
            }
            if (previous.Links.SequenceEqual(links, StringComparer.Ordinal))
                return;
            var change = new SocialChange
            {
                Id = key + ":" + previous.Version.ToString(CultureInfo.InvariantCulture),
                Name = address,
                Network = network,
                ContractAddress = address,
                Source = "DEX Screener",
                SourceUrl = $"https://dexscreener.com/{Uri.EscapeDataString(network)}/{Uri.EscapeDataString(address)}",
                ObservedAt = DateTime.UtcNow,
                PreviousLinks = previous.Links,
                CurrentLinks = links,
            };
            // The new links and their pending activity commit together in one document.
            UpdateDefinition<SocialSnapshot> update = Builders<SocialSnapshot>.Update
                .Set(x => x.Links, links).Inc(x => x.Version, 1).Set(x => x.PendingChange, change);
            UpdateResult result = await snapshots.UpdateOneAsync(x => x.Id == key && x.Version == previous.Version && x.PendingChange == null,
                update, cancellationToken: cancellationToken);
            if (result.ModifiedCount == 0)
                continue;
            await FlushPendingAsync(snapshots, changes,
                new SocialSnapshot { Id = key, Version = previous.Version + 1, PendingChange = change }, cancellationToken);
            return;
        }
    }

    private static async Task FlushPendingAsync(IMongoCollection<SocialSnapshot> snapshots, IMongoCollection<SocialChange> changes,
        SocialSnapshot snapshot, CancellationToken cancellationToken)
    {
        SocialChange change = snapshot.PendingChange!;
        // Replacing the same stable ID is safe after a crash between publication and acknowledgment.
        try
        {
            await changes.ReplaceOneAsync(x => x.Id == change.Id, change, new ReplaceOptions { IsUpsert = true }, cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // A concurrent flush may have inserted this same pending event first.
            if (!await changes.Find(x => x.Id == change.Id).AnyAsync(cancellationToken))
                throw;
        }
        await snapshots.UpdateOneAsync(x => x.Id == snapshot.Id && x.Version == snapshot.Version && x.PendingChange!.Id == change.Id,
            Builders<SocialSnapshot>.Update.Unset(x => x.PendingChange), cancellationToken: cancellationToken);
    }
}
