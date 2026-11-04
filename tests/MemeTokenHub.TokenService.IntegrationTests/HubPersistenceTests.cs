using System.Net;
using System.Text;
using MemeTokenHub.TokenService.Api.Configuration;
using MemeTokenHub.TokenService.Api.Hub;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
namespace MemeTokenHub.TokenService.IntegrationTests;

public sealed class HubPersistenceTests
{
    [Test]
    public async Task ConcurrentMongoEditsPreserveTheWinningRevision()
    {
        string? connection = Environment.GetEnvironmentVariable("MTH_TEST_MONGO");
        if (connection is null)
        { Assert.Ignore("Set MTH_TEST_MONGO to run MongoDB persistence tests."); return; }
        var client = new MongoClient(connection);
        string database = "HubTest" + Guid.NewGuid().ToString("N");
        try
        {
            var repository = new MongoHubRepository(client, Options.Create(new MongoDbOptions { ConnectionString = connection, DatabaseName = database }));
            var original = new HubEntry { Kind = "upcoming", OwnerId = "owner", Payload = "{}" };
            await repository.CreateAsync(original, CancellationToken.None);
            HubEntry first = (await repository.GetAsync(original.Id, CancellationToken.None))!;
            HubEntry second = (await repository.GetAsync(original.Id, CancellationToken.None))!;
            first.Version = 2;
            first.Payload = "{\"name\":\"winner\"}";
            second.Version = 2;
            second.Payload = "{\"name\":\"stale\"}";
            Assert.That(await repository.ReplaceAsync(first, 1, CancellationToken.None), Is.True);
            Assert.That(await repository.ReplaceAsync(second, 1, CancellationToken.None), Is.False);
            Assert.That((await repository.GetAsync(original.Id, CancellationToken.None))!.Payload, Does.Contain("winner"));
        }
        finally { await client.DropDatabaseAsync(database); }
    }
    [Test]
    public async Task CollectorEstablishesBaselineAndEmitsOnlyChangedSocialLinks()
    {
        string? connection = Environment.GetEnvironmentVariable("MTH_TEST_MONGO");
        if (connection is null)
        { Assert.Ignore("Set MTH_TEST_MONGO to run MongoDB persistence tests."); return; }
        var client = new MongoClient(connection);
        string database = "HubTest" + Guid.NewGuid().ToString("N");
        try
        {
            var handler = new HubSourceHandler();
            var collector = new SocialActivityCollector(new HubClientFactory(handler), client,
                Options.Create(new MongoDbOptions { ConnectionString = connection, DatabaseName = database }),
                new ConfigurationBuilder().Build(), NullLogger<SocialActivityCollector>.Instance);
            IMongoCollection<SocialChange> changes = client.GetDatabase(database).GetCollection<SocialChange>("HubSocialChanges");
            await collector.CollectAsync(CancellationToken.None);
            Assert.That(await changes.CountDocumentsAsync(Builders<SocialChange>.Filter.Empty), Is.Zero);
            await collector.CollectAsync(CancellationToken.None);
            Assert.That(await changes.CountDocumentsAsync(Builders<SocialChange>.Filter.Empty), Is.Zero);
            handler.Url = "https://x.com/updated";
            await collector.CollectAsync(CancellationToken.None);
            SocialChange change = await changes.Find(Builders<SocialChange>.Filter.Empty).SingleAsync();
            Assert.Multiple(() =>
            {
                Assert.That(change.Source, Is.EqualTo("DEX Screener"));
                Assert.That(change.PreviousLinks.Single(), Is.EqualTo("https://x.com/original"));
                Assert.That(change.CurrentLinks.Single(), Is.EqualTo("https://x.com/updated"));
            });
            await collector.CollectAsync(CancellationToken.None);
            Assert.That(await changes.CountDocumentsAsync(Builders<SocialChange>.Filter.Empty), Is.EqualTo(1));
        }
        finally { await client.DropDatabaseAsync(database); }
    }
}
