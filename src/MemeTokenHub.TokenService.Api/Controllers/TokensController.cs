using System.Security.Claims;
using MemeTokenHub.TokenService.Api.Application.Abstractions;
using MemeTokenHub.TokenService.Api.Application.Models;
using MemeTokenHub.TokenService.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MemeTokenHub.TokenService.Api.Controllers;

[ApiController]
[Route("api/tokens")]
[Produces("application/json")]
public sealed class TokensController : ControllerBase
{
    private readonly ITokenService _tokenService;
    private readonly IMediaUploadService _mediaUploadService;

    public TokensController(ITokenService tokenService, IMediaUploadService mediaUploadService)
    {
        _tokenService = tokenService;
        _mediaUploadService = mediaUploadService;
    }

    [HttpGet]
    [ProducesResponseType<PagedResult<TokenResponse>>(StatusCodes.Status200OK)]
    public Task<PagedResult<TokenResponse>> SearchAsync(
        [FromQuery] string? search,
        [FromQuery] string? network,
        [FromQuery] string? category,
        [FromQuery] string? creatorId,
        [FromQuery] TokenStatus? status,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default) =>
        _tokenService.SearchAsync(new TokenQuery
        {
            Search = search,
            Network = network,
            Category = category,
            CreatorId = creatorId,
            Status = status,
            SortBy = sortBy,
            Limit = limit,
            Offset = offset,
        }, cancellationToken);

    [Authorize(Policy = "ProjectsWrite")]
    [HttpPost("media/upload-url")]
    [ProducesResponseType<MediaUploadResponse>(StatusCodes.Status200OK)]
    public ActionResult<MediaUploadResponse> CreateMediaUpload(MediaUploadRequest request) =>
        Ok(_mediaUploadService.CreateUpload(request, GetRequiredUserId()));

    [HttpGet("{tokenId}")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<TokenResponse> GetAsync(string tokenId, CancellationToken cancellationToken) =>
        _tokenService.GetAsync(tokenId, User.FindFirstValue(ClaimTypes.NameIdentifier), cancellationToken);

    [Authorize(Policy = "ProjectsWrite")]
    [HttpPost]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TokenResponse>> CreateAsync(CreateTokenRequest request, CancellationToken cancellationToken)
    {
        TokenResponse token = await _tokenService.CreateAsync(request, GetRequiredUserId(), cancellationToken);
        return CreatedAtAction(nameof(GetAsync), new { tokenId = token.TokenId }, token);
    }

    [Authorize(Policy = "ProjectsWrite")]
    [HttpPut("{tokenId}")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    public Task<TokenResponse> UpdateAsync(string tokenId, UpdateTokenRequest request, CancellationToken cancellationToken) =>
        _tokenService.UpdateAsync(tokenId, request, GetRequiredUserId(), User.IsInRole("Moderator"), cancellationToken);

    [Authorize(Policy = "ProjectsWrite")]
    [HttpPost("{tokenId}/publish")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    public Task<TokenResponse> PublishAsync(string tokenId, CancellationToken cancellationToken) =>
        _tokenService.PublishAsync(tokenId, GetRequiredUserId(), User.IsInRole("Moderator"), cancellationToken);

    [HttpGet("feeds/{type}")]
    [ProducesResponseType<PagedResult<TokenResponse>>(StatusCodes.Status200OK)]
    public Task<PagedResult<TokenResponse>> GetFeedAsync(string type, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        TokenStatus? status = type.ToLowerInvariant() switch
        {
            "unclaimed" => TokenStatus.Unclaimed,
            "featured" => TokenStatus.Featured,
            _ => null,
        };
        string sortBy = type.Equals("trending", StringComparison.OrdinalIgnoreCase) ? "popularity" : "createdAt";
        return _tokenService.SearchAsync(new TokenQuery { Status = status, SortBy = sortBy, Limit = limit }, cancellationToken);
    }

    [HttpGet("by-creator/{creatorId}")]
    public Task<PagedResult<TokenResponse>> GetByCreatorAsync(string creatorId, [FromQuery] int limit = 20, [FromQuery] int offset = 0, CancellationToken cancellationToken = default) =>
        _tokenService.SearchAsync(new TokenQuery { CreatorId = creatorId, Limit = limit, Offset = offset }, cancellationToken);

    [HttpGet("networks")]
    public Task<IReadOnlyCollection<NetworkResponse>> GetNetworksAsync(CancellationToken cancellationToken) =>
        _tokenService.GetNetworksAsync(cancellationToken);

    [HttpGet("{tokenId}/sentiment")]
    public Task<SentimentResponse> GetSentimentAsync(string tokenId, CancellationToken cancellationToken) =>
        _tokenService.GetSentimentAsync(tokenId, cancellationToken);

    [HttpGet("analytics/{tokenId}")]
    public Task<SentimentResponse> GetAnalyticsAsync(string tokenId, CancellationToken cancellationToken) =>
        _tokenService.GetSentimentAsync(tokenId, cancellationToken);

    private string GetRequiredUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("The authenticated user has no subject identifier.");
}
