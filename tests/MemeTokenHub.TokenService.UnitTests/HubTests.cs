using System.Security.Claims;
using System.Text.Json;
using MemeTokenHub.TokenService.Api.Domain;
using MemeTokenHub.TokenService.Api.Hub;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
namespace MemeTokenHub.TokenService.UnitTests;

public sealed class HubTests
{
    private static JsonElement Data(string contract = "", string trading = "", string network = "Solana") => JsonSerializer.SerializeToElement(new
    {
        name = "Orbit",
        symbol = "ORBIT",
        network,
        story = "The project story",
        distribution = "Community allocation",
        disclosures = "No affiliations",
        launchAt = "2027-01-01T14:00:00Z",
        launchpad = "Example",
        launchType = "Fair launch",
        logoUrl = "https://example.com/logo.png",
        websiteUrl = "https://example.com",
        telegramUrl = "",
        contractAddress = contract,
        tradingUrl = trading,
    });
    private static HubController Controller(IHubRepository repository, FakeTokenRepository tokens, string userId = "owner") => new(repository, tokens)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId), new Claim("capability", "projects:write")], "test")) } },
    };
    private static async Task<HubEntry> Create(FakeHubRepository repository, FakeTokenRepository tokens)
    {
        IActionResult result = await Controller(repository, tokens).Create("upcoming", new(Data()), CancellationToken.None);
        Assert.That(result, Is.TypeOf<CreatedResult>());
        return (await repository.ListAsync("upcoming", null, CancellationToken.None)).Single();
    }
    [Test]
    public async Task LifecycleCreatesOnePermanentTokenAndRetainsEveryRevision()
    {
        var repository = new FakeHubRepository();
        var tokens = new FakeTokenRepository();
        HubEntry entry = await Create(repository, tokens);
        HubController controller = Controller(repository, tokens);
        foreach (long version in new long[] { 1, 2, 3 })
            Assert.That(await controller.Advance(entry.Id, new(Data("contract-1", "https://example.com/trade"), version), CancellationToken.None), Is.TypeOf<OkObjectResult>());
        HubEntry saved = (await repository.GetAsync(entry.Id, CancellationToken.None))!;
        Assert.Multiple(() =>
        {
            Assert.That(saved.Stage, Is.EqualTo("MTH profile created"));
            Assert.That(saved.History.Select(x => x.Stage), Is.EqualTo((string[])["Upcoming", "Contract published", "Trading live", "MTH profile created"]));
            Assert.That(saved.History[0].Payload, Does.Contain("\"contractAddress\":\"\""));
            Assert.That(tokens.Tokens, Has.Count.EqualTo(1));
            Assert.That(tokens.Tokens[0].TokenId, Is.EqualTo(saved.TokenId));
            Assert.That(tokens.Tokens[0].Status, Is.EqualTo(TokenStatus.Unclaimed));
        });
        Assert.That(await controller.Advance(entry.Id, new(Data("contract-1"), saved.Version), CancellationToken.None), Is.TypeOf<OkObjectResult>());
        Assert.That(tokens.Tokens, Has.Count.EqualTo(1));
    }
    [Test]
    public async Task WhitespaceIsNormalizedBeforePinningAndTokenCreation()
    {
        var repository = new FakeHubRepository();
        var tokens = new FakeTokenRepository();
        HubController controller = Controller(repository, tokens);
        Assert.That(await controller.Create("upcoming", new(Data(network: " Solana ")), CancellationToken.None), Is.TypeOf<CreatedResult>());
        HubEntry entry = (await repository.ListAsync("upcoming", null, CancellationToken.None)).Single();
        Assert.That(JsonSerializer.Deserialize<UpcomingProfileData>(entry.Payload, HubValidation.JsonOptions)!.Network, Is.EqualTo("Solana"));
        Assert.That(await controller.Update("upcoming", entry.Id, new(Data(network: "  Solana  "), 1), CancellationToken.None), Is.TypeOf<OkObjectResult>());
        foreach (long version in new long[] { 2, 3, 4 })
            Assert.That(await controller.Advance(entry.Id, new(Data(" contract-1 ", "https://example.com/trade", " Solana "), version), CancellationToken.None), Is.TypeOf<OkObjectResult>());
        HubEntry saved = (await repository.GetAsync(entry.Id, CancellationToken.None))!;
        UpcomingProfileData published = JsonSerializer.Deserialize<UpcomingProfileData>(saved.Payload, HubValidation.JsonOptions)!;
        Assert.Multiple(() =>
        {
            Assert.That(tokens.Tokens.Single().Network, Is.EqualTo("Solana"));
            Assert.That(tokens.Tokens.Single().ContractAddress, Is.EqualTo("contract-1"));
            Assert.That(published.Network, Is.EqualTo("Solana"));
            Assert.That(published.ContractAddress, Is.EqualTo("contract-1"));
        });
    }
    [Test]
    public async Task PreviouslyPinnedWhitespaceCannotBypassAnExistingContract()
    {
        var repository = new FakeHubRepository();
        var tokens = new FakeTokenRepository();
        HubEntry entry = await Create(repository, tokens);
        HubController controller = Controller(repository, tokens);
        await controller.Advance(entry.Id, new(Data("contract-1", "https://example.com/trade"), 1), CancellationToken.None);
        await controller.Advance(entry.Id, new(Data("contract-1", "https://example.com/trade"), 2), CancellationToken.None);
        // Simulate a profile pinned by the earlier, unnormalized implementation.
        HubEntry legacy = (await repository.GetAsync(entry.Id, CancellationToken.None))!;
        legacy.Payload = Data(" contract-1 ", "https://example.com/trade", " Solana ").GetRawText();
        await repository.ReplaceAsync(legacy, legacy.Version, CancellationToken.None);
        var existing = new TokenProject
        {
            TokenId = "existing-token",
            Name = "Existing",
            Symbol = "EXIST",
            Description = "Existing project",
            Network = "Solana",
            ContractAddress = "contract-1",
            Category = "Meme",
            CreatorId = "another-owner",
        };
        await tokens.TryCreateAsync(existing, CancellationToken.None);
        IActionResult result = await controller.Advance(entry.Id, new(Data("contract-1", "https://example.com/trade"), 3), CancellationToken.None);
        Assert.That(result, Is.TypeOf<ConflictObjectResult>());
        Assert.That(tokens.Tokens, Has.Count.EqualTo(1));
        Assert.That((await repository.GetAsync(entry.Id, CancellationToken.None))!.Stage, Is.EqualTo("Trading live"));
    }
    [TestCase(" Solana ", " contract-1 ", true)]
    [TestCase("Other network", "contract-1", false)]
    public async Task ConversionRetryComparesNormalizedNetworkAndContract(string network, string contract, bool matches)
    {
        var repository = new FakeHubRepository();
        var tokens = new FakeTokenRepository();
        HubEntry entry = await Create(repository, tokens);
        HubController controller = Controller(repository, tokens);
        await controller.Advance(entry.Id, new(Data("contract-1", "https://example.com/trade"), 1), CancellationToken.None);
        await controller.Advance(entry.Id, new(Data("contract-1", "https://example.com/trade"), 2), CancellationToken.None);
        // The canonical insert succeeded, but the preceding process did not save the Hub link.
        await tokens.TryCreateAsync(new TokenProject
        {
            TokenId = $"launch-{entry.Id}",
            Name = "Orbit",
            Symbol = "ORBIT",
            Description = "Existing canonical record",
            Network = network,
            ContractAddress = contract,
            Category = "Meme",
            CreatorId = "owner",
        }, CancellationToken.None);
        // Force the insert to conflict on the deterministic token ID, as MongoDB would.
        var conflictingTokens = new FakeTokenRepository { ForceCreateConflict = true };
        conflictingTokens.Tokens.AddRange(tokens.Tokens);
        IActionResult result = await Controller(repository, conflictingTokens).Advance(entry.Id, new(Data("contract-1", "https://example.com/trade"), 3), CancellationToken.None);
        Assert.That(result, matches ? Is.TypeOf<OkObjectResult>() : Is.TypeOf<ConflictObjectResult>());
        Assert.That(conflictingTokens.Tokens, Has.Count.EqualTo(1));
    }
    [Test]
    public async Task AnotherCreatorCannotEditOrAdvanceAProfile()
    {
        var repository = new FakeHubRepository();
        var tokens = new FakeTokenRepository();
        HubEntry entry = await Create(repository, tokens);
        HubController other = Controller(repository, tokens, "other");
        Assert.That(await other.Update("upcoming", entry.Id, new(Data(), 1), CancellationToken.None), Is.TypeOf<ForbidResult>());
        Assert.That(await other.Advance(entry.Id, new(Data("contract-1"), 1), CancellationToken.None), Is.TypeOf<ForbidResult>());
        Assert.That((await repository.GetAsync(entry.Id, CancellationToken.None))!.Version, Is.EqualTo(1));
    }
    [Test]
    public async Task StaleRevisionAndMissingTradingLinkDoNotAdvance()
    {
        var repository = new FakeHubRepository();
        var tokens = new FakeTokenRepository();
        HubEntry entry = await Create(repository, tokens);
        HubController controller = Controller(repository, tokens);
        Assert.That(await controller.Advance(entry.Id, new(Data("contract-1"), 0), CancellationToken.None), Is.TypeOf<ConflictObjectResult>());
        await controller.Advance(entry.Id, new(Data("contract-1"), 1), CancellationToken.None);
        Assert.That(await controller.Advance(entry.Id, new(Data("contract-1"), 2), CancellationToken.None), Is.TypeOf<BadRequestObjectResult>());
        Assert.That((await repository.GetAsync(entry.Id, CancellationToken.None))!.Stage, Is.EqualTo("Contract published"));
        Assert.That(tokens.Tokens, Is.Empty);
    }
    [Test]
    public async Task PublishedContractCannotBeReplaced()
    {
        var repository = new FakeHubRepository();
        var tokens = new FakeTokenRepository();
        HubEntry entry = await Create(repository, tokens);
        HubController controller = Controller(repository, tokens);
        await controller.Advance(entry.Id, new(Data("contract-1"), 1), CancellationToken.None);
        Assert.That(await controller.Update("upcoming", entry.Id, new(Data("contract-2"), 2), CancellationToken.None), Is.TypeOf<BadRequestObjectResult>());
        Assert.That(await controller.Advance(entry.Id, new(Data("contract-2", "https://example.com/trade"), 2), CancellationToken.None), Is.TypeOf<BadRequestObjectResult>());
    }
    [Test]
    public async Task PrivateListingApplicationIsNotPubliclyReadable()
    {
        var repository = new FakeHubRepository();
        var tokens = new FakeTokenRepository();
        var application = new HubEntry { Kind = "applications", OwnerId = "owner", Payload = "{}" };
        await repository.CreateAsync(application, CancellationToken.None);
        Assert.That(await Controller(repository, tokens, "other").Get("applications", application.Id, CancellationToken.None), Is.TypeOf<NotFoundResult>());
    }
    [Test]
    public void SummaryMustBeReviewedByTheOwner()
    {
        JsonElement data = JsonSerializer.SerializeToElement(new { title = "Space", subject = "Launch", host = "Host", startsAt = "2027-01-01T14:00:00Z", eventUrl = "https://example.com", summary = "Unreviewed summary", ownerReviewed = false });
        Assert.That(HubValidation.Validate("events", data), Does.Contain("owner must review"));
    }
    [TestCase("javascript:alert(1)")]
    [TestCase("http://example.com")]
    [TestCase("https://user:password@example.com")]
    [TestCase("https://localhost")]
    public void UnsafeLinksAreRejected(string url) => Assert.That(HubValidation.IsPublicHttpsUrl(url), Is.False);
}
