using MemeTokenHub.TokenService.Api.Application.Abstractions;
using MemeTokenHub.TokenService.Api.Application.Models;
using MemeTokenHub.TokenService.Api.Domain;

namespace MemeTokenHub.TokenService.Api.Application.Services;

public sealed class TokenService : ITokenService
{
    private readonly ITokenRepository _repository;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes the token application service with persistence and time abstractions.
    /// </summary>
    public TokenService(ITokenRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    /// <summary>
    /// Validates and creates a private token draft, returning a conflict for duplicate contracts.
    /// </summary>
    public async Task<TokenResponse> CreateAsync(CreateTokenRequest request, string creatorId, CancellationToken cancellationToken)
    {
        ValidateUrls(request.LogoUrl, request.BannerUrl, request.WebsiteUrl, request.SocialLinks);
        string network = request.Network.Trim();
        string contractAddress = request.ContractAddress.Trim();
        if (await _repository.ContractExistsAsync(network, contractAddress, cancellationToken))
        {
            throw new ApiException(StatusCodes.Status409Conflict, "Contract already listed", "A token with this contract address already exists on the selected network.");
        }

        DateTime now = _clock.UtcNow;
        var token = new TokenProject
        {
            TokenId = Guid.NewGuid().ToString("N"),
            Name = request.Name.Trim(),
            Symbol = request.Symbol.Trim().ToUpperInvariant(),
            Description = request.Description.Trim(),
            Network = network,
            ContractAddress = contractAddress,
            Category = request.Category.Trim(),
            CreatorId = creatorId,
            LogoUrl = request.LogoUrl,
            BannerUrl = request.BannerUrl,
            WebsiteUrl = request.WebsiteUrl,
            SocialLinks = request.SocialLinks,
            CreatedAt = now,
            UpdatedAt = now,
        };

        if (!await _repository.TryCreateAsync(token, cancellationToken))
        {
            throw ContractConflict();
        }

        return Map(token);
    }

    /// <summary>
    /// Gets a token while enforcing draft visibility for the requesting user.
    /// </summary>
    public async Task<TokenResponse> GetAsync(string tokenId, string? requesterId, CancellationToken cancellationToken)
    {
        TokenProject? token = await _repository.GetByIdAsync(tokenId, includeDrafts: requesterId is not null, cancellationToken);
        if (token is null || (token.LaunchStatus != LaunchStatus.Published && token.CreatorId != requesterId))
        {
            throw NotFound(tokenId);
        }

        return Map(token);
    }

    /// <summary>
    /// Searches public token projections after validating pagination constraints.
    /// </summary>
    public async Task<PagedResult<TokenResponse>> SearchAsync(TokenQuery query, CancellationToken cancellationToken)
    {
        if (query.Limit is < 1 or > 100 || query.Offset < 0)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "Invalid pagination", "Limit must be between 1 and 100 and offset cannot be negative.");
        }

        PagedResult<TokenProject> result = await _repository.SearchAsync(query, includeDrafts: false, cancellationToken);
        return new PagedResult<TokenResponse>
        {
            Items = result.Items.Select(Map).ToArray(),
            Limit = result.Limit,
            Offset = result.Offset,
            Total = result.Total,
        };
    }

    /// <summary>
    /// Updates editable token metadata for an owner or moderator using optimistic concurrency.
    /// </summary>
    public async Task<TokenResponse> UpdateAsync(string tokenId, UpdateTokenRequest request, string requesterId, bool isModerator, CancellationToken cancellationToken)
    {
        ValidateUrls(request.LogoUrl, request.BannerUrl, request.WebsiteUrl, request.SocialLinks);
        TokenProject token = await GetOwnedTokenAsync(tokenId, requesterId, isModerator, cancellationToken);
        token.Name = request.Name.Trim();
        token.Description = request.Description.Trim();
        token.Category = request.Category.Trim();
        token.LogoUrl = request.LogoUrl;
        token.BannerUrl = request.BannerUrl;
        token.WebsiteUrl = request.WebsiteUrl;
        token.SocialLinks = request.SocialLinks;
        token.UpdatedAt = _clock.UtcNow;
        await SaveAsync(token, cancellationToken);
        return Map(token);
    }

    /// <summary>
    /// Idempotently publishes an owned token draft using the authoritative server time.
    /// </summary>
    public async Task<TokenResponse> PublishAsync(string tokenId, string requesterId, bool isModerator, CancellationToken cancellationToken)
    {
        TokenProject token = await GetOwnedTokenAsync(tokenId, requesterId, isModerator, cancellationToken);
        if (token.LaunchStatus == LaunchStatus.Published)
        {
            return Map(token);
        }

        DateTime now = _clock.UtcNow;
        token.LaunchStatus = LaunchStatus.Published;
        token.PublishedAt ??= now;
        token.UpdatedAt = now;
        await SaveAsync(token, cancellationToken);
        return Map(token);
    }

    /// <summary>
    /// Gets network names and published token counts from the discovery projection.
    /// </summary>
    public Task<IReadOnlyCollection<NetworkResponse>> GetNetworksAsync(CancellationToken cancellationToken) =>
        _repository.GetNetworksAsync(cancellationToken);

    /// <summary>
    /// Gets the cached social sentiment projection for a published token.
    /// </summary>
    public async Task<SentimentResponse> GetSentimentAsync(string tokenId, CancellationToken cancellationToken)
    {
        TokenProject? token = await _repository.GetByIdAsync(tokenId, includeDrafts: false, cancellationToken);
        if (token is null)
        {
            throw NotFound(tokenId);
        }

        return new SentimentResponse
        {
            TokenId = token.TokenId,
            HotVotes = token.Community.HotVotes,
            NotVotes = token.Community.NotVotes,
            SentimentScore = token.Community.SentimentScore,
            UpdatedAt = token.Community.UpdatedAt,
        };
    }

    /// <summary>
    /// Loads a token and verifies that the requester can modify it.
    /// </summary>
    private async Task<TokenProject> GetOwnedTokenAsync(string tokenId, string requesterId, bool isModerator, CancellationToken cancellationToken)
    {
        TokenProject? token = await _repository.GetByIdAsync(tokenId, includeDrafts: true, cancellationToken);
        if (token is null)
        {
            throw NotFound(tokenId);
        }

        if (!isModerator && token.CreatorId != requesterId)
        {
            throw new ApiException(StatusCodes.Status403Forbidden, "Forbidden", "Only the project owner or a moderator can change this token.");
        }

        return token;
    }

    /// <summary>
    /// Persists a token with optimistic concurrency and translates stale writes into conflicts.
    /// </summary>
    private async Task SaveAsync(TokenProject token, CancellationToken cancellationToken)
    {
        if (!await _repository.TryUpdateAsync(token, cancellationToken))
        {
            throw new ApiException(StatusCodes.Status409Conflict, "Token changed", "The token was changed by another request. Reload it and retry.");
        }
    }

    /// <summary>
    /// Creates the standard conflict returned for an already-listed network contract.
    /// </summary>
    private static ApiException ContractConflict() =>
        new(StatusCodes.Status409Conflict, "Contract already listed", "A token with this contract address already exists on the selected network.");

    /// <summary>
    /// Ensures all supplied project, media, and social URLs are absolute HTTPS URLs.
    /// </summary>
    private static void ValidateUrls(string? logoUrl, string? bannerUrl, string? websiteUrl, IEnumerable<SocialLink> socialLinks)
    {
        IEnumerable<string?> urls = new[] { logoUrl, bannerUrl, websiteUrl }.Concat(socialLinks.Select(link => link.Url));
        if (urls.Where(url => !string.IsNullOrWhiteSpace(url)).Any(url => !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ApiException(StatusCodes.Status422UnprocessableEntity, "Invalid URL", "All project and media URLs must use HTTPS.");
        }
    }

    /// <summary>
    /// Creates the standard not-found response without revealing hidden drafts.
    /// </summary>
    private static ApiException NotFound(string tokenId) =>
        new(StatusCodes.Status404NotFound, "Token not found", $"Token '{tokenId}' was not found.");

    /// <summary>
    /// Maps the persistence model to the public API response model.
    /// </summary>
    private static TokenResponse Map(TokenProject token) => new()
    {
        TokenId = token.TokenId,
        Name = token.Name,
        Symbol = token.Symbol,
        Description = token.Description,
        Network = token.Network,
        ContractAddress = token.ContractAddress,
        Category = token.Category,
        CreatorId = token.CreatorId,
        LogoUrl = token.LogoUrl,
        BannerUrl = token.BannerUrl,
        WebsiteUrl = token.WebsiteUrl,
        SocialLinks = token.SocialLinks,
        Status = token.Status,
        LaunchStatus = token.LaunchStatus,
        Metadata = token.Metadata,
        Community = token.Community,
        CreatedAt = token.CreatedAt,
        UpdatedAt = token.UpdatedAt,
        PublishedAt = token.PublishedAt,
        UpcomingProfileId = token.UpcomingProfileId,
    };
}
