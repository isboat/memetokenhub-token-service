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
        using HttpClient client = clients.CreateClient("HubSources");
        using HttpResponseMessage response = await client.GetAsync("https://api.dexscreener.com/token-profiles/latest/v1", cancellationToken);
        response.EnsureSuccessStatusCode();
        using JsonDocument document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        IMongoDatabase database = mongo.GetDatabase(options.Value.DatabaseName);
        IMongoCollection<SocialSnapshot> snapshots = database.GetCollection<SocialSnapshot>("HubSocialSnapshots");
        IMongoCollection<SocialChange> changes = database.GetCollection<SocialChange>("HubSocialChanges");
        foreach (JsonElement row in document.RootElement.EnumerateArray().Take(100))
        {
            string address = row.GetProperty("tokenAddress").GetString() ?? "";
            string network = row.GetProperty("chainId").GetString() ?? "";
            if (address.Length == 0 || network.Length == 0)
                continue;
            string[] links = row.TryGetProperty("links", out JsonElement suppliedLinks)
                ? suppliedLinks.EnumerateArray().Where(x => x.TryGetProperty("type", out _)).Select(x => x.GetProperty("url").GetString() ?? "").Where(HubValidation.IsPublicHttpsUrl).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
                : [];
            string key = $"dexscreener:{network}:{address}";
            UpdateDefinition<SocialSnapshot> update = Builders<SocialSnapshot>.Update.Set(x => x.Links, links).Inc(x => x.Version, 1);
            SocialSnapshot? previous = await snapshots.FindOneAndUpdateAsync(x => x.Id == key, update,
                new FindOneAndUpdateOptions<SocialSnapshot> { IsUpsert = true, ReturnDocument = ReturnDocument.Before }, cancellationToken);
            if (previous is null || previous.Links.SequenceEqual(links, StringComparer.Ordinal))
                continue;
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
            await changes.InsertOneAsync(change, cancellationToken: cancellationToken);
        }
    }
}
