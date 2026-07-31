using MemeTokenHub.TokenService.Api.Application.Abstractions;
using MemeTokenHub.TokenService.Api.Application.Models;
using MemeTokenHub.TokenService.Api.Domain;

namespace MemeTokenHub.TokenService.Api.Application.Services;

public sealed class TokenService : ITokenService
{
    private readonly ITokenRepository _repository;
    private readonly IClock _clock;

    public TokenService(ITokenRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

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

    public async Task<TokenResponse> GetAsync(string tokenId, string? requesterId, CancellationToken cancellationToken)
    {
        TokenProject? token = await _repository.GetByIdAsync(tokenId, includeDrafts: requesterId is not null, cancellationToken);
        if (token is null || (token.LaunchStatus != LaunchStatus.Published && token.CreatorId != requesterId))
        {
            throw NotFound(tokenId);
        }

        return Map(token);
    }

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

    public Task<IReadOnlyCollection<NetworkResponse>> GetNetworksAsync(CancellationToken cancellationToken) =>
        _repository.GetNetworksAsync(cancellationToken);

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

    private async Task SaveAsync(TokenProject token, CancellationToken cancellationToken)
    {
        if (!await _repository.TryUpdateAsync(token, cancellationToken))
        {
            throw new ApiException(StatusCodes.Status409Conflict, "Token changed", "The token was changed by another request. Reload it and retry.");
        }
    }

    private static ApiException ContractConflict() =>
        new(StatusCodes.Status409Conflict, "Contract already listed", "A token with this contract address already exists on the selected network.");

    private static void ValidateUrls(string? logoUrl, string? bannerUrl, string? websiteUrl, IEnumerable<SocialLink> socialLinks)
    {
        IEnumerable<string?> urls = new[] { logoUrl, bannerUrl, websiteUrl }.Concat(socialLinks.Select(link => link.Url));
        if (urls.Where(url => !string.IsNullOrWhiteSpace(url)).Any(url => !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ApiException(StatusCodes.Status422UnprocessableEntity, "Invalid URL", "All project and media URLs must use HTTPS.");
        }
    }

    private static ApiException NotFound(string tokenId) =>
        new(StatusCodes.Status404NotFound, "Token not found", $"Token '{tokenId}' was not found.");

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
    };
}
