using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using MemeTokenHub.TokenService.Api.Application.Models;
using MemeTokenHub.TokenService.Api.Configuration;
using MemeTokenHub.TokenService.Api.Domain;
using MemeTokenHub.TokenService.Api.Hub;
using MemeTokenHub.TokenService.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
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
    [Test]
    public async Task FailedActivityWriteRecoversAfterRestartEvenWhenTheSourceIsUnavailable()
    {
        string? connection = Environment.GetEnvironmentVariable("MTH_TEST_MONGO");
        if (connection is null)
        { Assert.Ignore("Set MTH_TEST_MONGO to run MongoDB persistence tests."); return; }
        var client = new MongoClient(connection);
        string databaseName = "HubTest" + Guid.NewGuid().ToString("N");
        try
        {
            IMongoDatabase database = client.GetDatabase(databaseName);
            var handler = new HubSourceHandler();
            SocialActivityCollector Collector() => new(new HubClientFactory(handler), client,
                Options.Create(new MongoDbOptions { ConnectionString = connection, DatabaseName = databaseName }),
                new ConfigurationBuilder().Build(), NullLogger<SocialActivityCollector>.Instance);
            await Collector().CollectAsync(CancellationToken.None);
            // Real MongoDB validation rejects the activity write, after the snapshot commits.
            await database.RunCommandAsync<BsonDocument>(new BsonDocument
            {
                { "create", "HubSocialChanges" }, { "validator", new BsonDocument("allowWrite", true) },
            });
            handler.Url = "https://x.com/updated";
            Assert.CatchAsync<MongoException>(() => Collector().CollectAsync(CancellationToken.None));
            IMongoCollection<SocialSnapshot> snapshots = database.GetCollection<SocialSnapshot>("HubSocialSnapshots");
            SocialSnapshot snapshot = await snapshots.Find(Builders<SocialSnapshot>.Filter.Empty).SingleAsync();
            SocialChange pending = snapshot.PendingChange!;
            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Links.Single(), Is.EqualTo("https://x.com/updated"));
                Assert.That(pending, Is.Not.Null);
                Assert.That(pending.PreviousLinks.Single(), Is.EqualTo("https://x.com/original"));
                Assert.That(pending.CurrentLinks.Single(), Is.EqualTo("https://x.com/updated"));
            });
            await database.RunCommandAsync<BsonDocument>(new BsonDocument
            {
                { "collMod", "HubSocialChanges" }, { "validator", new BsonDocument() },
            });
            handler.StatusCode = HttpStatusCode.ServiceUnavailable;
            // A new collector must drain durable work before attempting another source request.
            Assert.ThrowsAsync<HttpRequestException>(() => Collector().CollectAsync(CancellationToken.None));
            IMongoCollection<SocialChange> changes = database.GetCollection<SocialChange>("HubSocialChanges");
            SocialChange recovered = await changes.Find(Builders<SocialChange>.Filter.Empty).SingleAsync();
            Assert.Multiple(() =>
            {
                Assert.That(recovered.Id, Is.EqualTo(pending.Id));
                Assert.That(recovered.ObservedAt, Is.EqualTo(pending.ObservedAt));
                Assert.That(recovered.PreviousLinks, Is.EqualTo(pending.PreviousLinks));
                Assert.That(recovered.CurrentLinks, Is.EqualTo(pending.CurrentLinks));
            });
            Assert.That((await snapshots.Find(Builders<SocialSnapshot>.Filter.Empty).SingleAsync()).PendingChange, Is.Null);
            // Simulate a crash after writing the activity but before acknowledging its pending entry.
            await snapshots.UpdateOneAsync(x => x.Id == snapshot.Id, Builders<SocialSnapshot>.Update.Set(x => x.PendingChange, pending));
            handler.StatusCode = HttpStatusCode.OK;
            handler.EmptyFeed = true;
            await Collector().CollectAsync(CancellationToken.None);
            Assert.That(await changes.CountDocumentsAsync(Builders<SocialChange>.Filter.Empty), Is.EqualTo(1));
            Assert.That((await snapshots.Find(Builders<SocialSnapshot>.Filter.Empty).SingleAsync()).PendingChange, Is.Null);
        }
        finally { await client.DropDatabaseAsync(databaseName); }
    }
    [Test]
    public async Task ConcurrentCollectorsPublishOnlyOneActivityForTheSameChange()
    {
        string? connection = Environment.GetEnvironmentVariable("MTH_TEST_MONGO");
        if (connection is null)
        { Assert.Ignore("Set MTH_TEST_MONGO to run MongoDB persistence tests."); return; }
        var client = new MongoClient(connection);
        string database = "HubTest" + Guid.NewGuid().ToString("N");
        try
        {
            var handler = new HubSourceHandler();
            SocialActivityCollector Collector() => new(new HubClientFactory(handler), client,
                Options.Create(new MongoDbOptions { ConnectionString = connection, DatabaseName = database }),
                new ConfigurationBuilder().Build(), NullLogger<SocialActivityCollector>.Instance);
            await Task.WhenAll(Collector().CollectAsync(CancellationToken.None), Collector().CollectAsync(CancellationToken.None));
            handler.Url = "https://x.com/updated";
            await Task.WhenAll(Collector().CollectAsync(CancellationToken.None), Collector().CollectAsync(CancellationToken.None));
            IMongoDatabase stored = client.GetDatabase(database);
            Assert.That(await stored.GetCollection<SocialChange>("HubSocialChanges").CountDocumentsAsync(Builders<SocialChange>.Filter.Empty), Is.EqualTo(1));
            SocialSnapshot snapshot = await stored.GetCollection<SocialSnapshot>("HubSocialSnapshots").Find(Builders<SocialSnapshot>.Filter.Empty).SingleAsync();
            Assert.That(snapshot.Links.Single(), Is.EqualTo("https://x.com/updated"));
            Assert.That(snapshot.PendingChange, Is.Null);
        }
        finally { await client.DropDatabaseAsync(database); }
    }

    [Test]
    public async Task NormalizedConversionRespectsTheMongoUniqueContractIndex()
    {
        string? connection = Environment.GetEnvironmentVariable("MTH_TEST_MONGO");
        if (connection is null)
        { Assert.Ignore("Set MTH_TEST_MONGO to run MongoDB persistence tests."); return; }
        var client = new MongoClient(connection);
        string database = "HubTest" + Guid.NewGuid().ToString("N");
        try
        {
            var options = Options.Create(new MongoDbOptions { ConnectionString = connection, DatabaseName = database });
            await new MongoIndexInitializer(client, options).StartAsync(CancellationToken.None);
            var tokens = new MongoTokenRepository(client, options);
            var entries = new MongoHubRepository(client, options);
            var data = new UpcomingProfileData
            {
                Name = "Orbit",
                Symbol = "ORBIT",
                Network = " Solana ",
                ContractAddress = " contract-1 ",
                Story = "Project story",
                Distribution = "Allocation",
                Disclosures = "No affiliations",
                LaunchAt = DateTimeOffset.UtcNow,
                Launchpad = "Example",
                LaunchType = "Fair launch",
                LogoUrl = "https://example.com/logo.png",
                TradingUrl = "https://example.com/trade",
            };
            var entry = new HubEntry
            {
                Kind = "upcoming",
                OwnerId = "owner",
                Stage = "Trading live",
                Payload = JsonSerializer.Serialize(data, HubValidation.JsonOptions),
            };
            await entries.CreateAsync(entry, CancellationToken.None);
            await tokens.TryCreateAsync(new TokenProject
            {
                TokenId = "existing",
                Name = "Existing",
                Symbol = "EXIST",
                Description = "Existing token",
                Network = "Solana",
                ContractAddress = "contract-1",
                Category = "Meme",
                CreatorId = "other",
                LaunchStatus = LaunchStatus.Published,
            }, CancellationToken.None);
            var controller = new HubController(entries, tokens)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity([
                            new Claim(ClaimTypes.NameIdentifier, "owner"),
                            new Claim("capability", "projects:write"),
                        ], "test")),
                    },
                },
            };
            var request = new HubWriteRequest(JsonSerializer.SerializeToElement(data, HubValidation.JsonOptions), entry.Version);
            Assert.That(await controller.Advance(entry.Id, request, CancellationToken.None), Is.TypeOf<ConflictObjectResult>());
            IMongoCollection<TokenProject> collection = client.GetDatabase(database).GetCollection<TokenProject>("Tokens");
            Assert.That(await collection.CountDocumentsAsync(Builders<TokenProject>.Filter.Empty), Is.EqualTo(1));
            // After the conflict is removed, conversion stores normalized values and normal network search finds it.
            await collection.DeleteOneAsync(x => x.TokenId == "existing");
            Assert.That(await controller.Advance(entry.Id, request, CancellationToken.None), Is.TypeOf<OkObjectResult>());
            var results = await tokens.SearchAsync(new TokenQuery { Network = "Solana" }, false, CancellationToken.None);
            Assert.That(results.Items.Single().ContractAddress, Is.EqualTo("contract-1"));
        }
        finally { await client.DropDatabaseAsync(database); }
    }

}
