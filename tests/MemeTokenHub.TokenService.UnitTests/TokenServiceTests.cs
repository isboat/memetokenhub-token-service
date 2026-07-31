using MemeTokenHub.TokenService.Api.Application.Models;
using MemeTokenHub.TokenService.Api.Domain;
using ApplicationTokenService = MemeTokenHub.TokenService.Api.Application.Services.TokenService;

namespace MemeTokenHub.TokenService.UnitTests;

public sealed class TokenServiceTests
{
    private static readonly DateTime Now = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task CreateAsyncCreatesPrivateDraftWithNormalizedSymbol()
    {
        var repository = new FakeTokenRepository();
        var service = new ApplicationTokenService(repository, new FixedClock { UtcNow = Now });

        TokenResponse response = await service.CreateAsync(CreateRequest(), "creator-1", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(response.Symbol, Is.EqualTo("MEME"));
            Assert.That(response.LaunchStatus, Is.EqualTo(LaunchStatus.Draft));
            Assert.That(response.CreatorId, Is.EqualTo("creator-1"));
            Assert.That(response.CreatedAt, Is.EqualTo(Now));
        });
    }

    [Test]
    public void CreateAsyncRejectsNonHttpsProjectUrls()
    {
        var repository = new FakeTokenRepository();
        var service = new ApplicationTokenService(repository, new FixedClock { UtcNow = Now });
        CreateTokenRequest request = CreateRequest("http://unsafe.example/logo.png");

        ApiException? exception = Assert.ThrowsAsync<ApiException>(() => service.CreateAsync(request, "creator-1", CancellationToken.None));

        Assert.That(exception!.StatusCode, Is.EqualTo(422));
    }

    [Test]
    public async Task PublishAsyncIsIdempotentAndPreservesFirstPublicationTime()
    {
        var repository = new FakeTokenRepository();
        var service = new ApplicationTokenService(repository, new FixedClock { UtcNow = Now });
        TokenResponse draft = await service.CreateAsync(CreateRequest(), "creator-1", CancellationToken.None);

        TokenResponse first = await service.PublishAsync(draft.TokenId, "creator-1", false, CancellationToken.None);
        TokenResponse second = await service.PublishAsync(draft.TokenId, "creator-1", false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(first.LaunchStatus, Is.EqualTo(LaunchStatus.Published));
            Assert.That(first.PublishedAt, Is.EqualTo(Now));
            Assert.That(second.PublishedAt, Is.EqualTo(first.PublishedAt));
        });
    }

    [Test]
    public Task SearchAsyncRejectsLimitAboveMaximum()
    {
        var service = new ApplicationTokenService(new FakeTokenRepository(), new FixedClock { UtcNow = Now });

        ApiException? exception = Assert.ThrowsAsync<ApiException>(() => service.SearchAsync(new TokenQuery { Limit = 101 }, CancellationToken.None));

        Assert.That(exception!.StatusCode, Is.EqualTo(400));
        return Task.CompletedTask;
    }

    [Test]
    public void CreateAsyncReturnsConflictWhenInsertLosesUniqueContractRace()
    {
        var repository = new FakeTokenRepository { ForceCreateConflict = true };
        var service = new ApplicationTokenService(repository, new FixedClock { UtcNow = Now });

        ApiException? exception = Assert.ThrowsAsync<ApiException>(() => service.CreateAsync(CreateRequest(), "creator-1", CancellationToken.None));

        Assert.That(exception!.StatusCode, Is.EqualTo(409));
    }

    [Test]
    public async Task PublishAsyncReturnsConflictWhenTokenWasConcurrentlyChanged()
    {
        var repository = new FakeTokenRepository();
        var service = new ApplicationTokenService(repository, new FixedClock { UtcNow = Now });
        TokenResponse draft = await service.CreateAsync(CreateRequest(), "creator-1", CancellationToken.None);
        var conflictingRepository = new FakeTokenRepository { ForceUpdateConflict = true };
        conflictingRepository.Tokens.Add(repository.Tokens.Single());
        var conflictingService = new ApplicationTokenService(conflictingRepository, new FixedClock { UtcNow = Now });

        ApiException? exception = Assert.ThrowsAsync<ApiException>(() => conflictingService.PublishAsync(draft.TokenId, "creator-1", false, CancellationToken.None));

        Assert.That(exception!.StatusCode, Is.EqualTo(409));
    }

    private static CreateTokenRequest CreateRequest(string? logoUrl = "https://cdn.example/logo.png") => new()
    {
        Name = "Readable Meme",
        Symbol = "meme",
        Description = "A community token project.",
        Network = "Solana",
        ContractAddress = "contract-1",
        Category = "Meme",
        LogoUrl = logoUrl,
    };
}
