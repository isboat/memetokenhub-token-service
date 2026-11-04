using System.Security.Claims;
using System.Text.Json;
using MemeTokenHub.TokenService.Api.Application.Abstractions;
using MemeTokenHub.TokenService.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace MemeTokenHub.TokenService.Api.Hub;

[ApiController]
[Route("api/hub")]
[Produces("application/json")]
public sealed class HubController(IHubRepository repository, ITokenRepository tokens) : ControllerBase
{
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private bool IsModerator => User.IsInRole("Moderator") || User.IsInRole("Admin");
    private bool CanWriteProjects => User.HasClaim("capability", "projects:write") || IsModerator;
    private bool CanWriteEvents => User.HasClaim("capability", "posts:write") || User.IsInRole("KOL") || User.IsInRole("Creator") || IsModerator;
    private static object View(HubEntry entry) => new
    {
        entry.Id,
        entry.Kind,
        entry.OwnerId,
        Data = JsonSerializer.Deserialize<JsonElement>(entry.Payload),
        entry.Stage,
        entry.TokenId,
        entry.Version,
        entry.UpdatedAt,
        History = entry.History.Select(x => new { x.Version, x.At, x.Stage, Data = JsonSerializer.Deserialize<JsonElement>(x.Payload) }),
    };
    [HttpGet("{kind}")]
    public async Task<IActionResult> List(string kind, CancellationToken cancellationToken)
    {
        if (kind is not ("upcoming" or "events" or "applications"))
            return NotFound();
        if (kind == "applications" && UserId is null)
            return Unauthorized();
        return Ok((await repository.ListAsync(kind, kind == "applications" && !IsModerator ? UserId : null, cancellationToken)).Select(View));
    }
    [HttpGet("{kind}/{id}")]
    public async Task<IActionResult> Get(string kind, string id, CancellationToken cancellationToken)
    {
        HubEntry? entry = await repository.GetAsync(id, cancellationToken);
        if (entry is null || entry.Kind != kind || (kind == "applications" && entry.OwnerId != UserId && !IsModerator))
            return NotFound();
        return Ok(View(entry));
    }
    [Authorize]
    [HttpPost("{kind}")]
    [RequestSizeLimit(30000)]
    public async Task<IActionResult> Create(string kind, HubWriteRequest request, CancellationToken cancellationToken)
    {
        if (UserId is null)
            return Unauthorized();
        if ((kind == "upcoming" && !CanWriteProjects) || (kind == "events" && !CanWriteEvents))
            return Forbid();
        string? error = HubValidation.Validate(kind, request.Data);
        if (error is not null)
            return BadRequest(new { detail = error });
        var entry = new HubEntry { Kind = kind, OwnerId = UserId, Payload = request.Data.GetRawText(), Stage = kind == "applications" ? "Pending review" : kind == "events" ? "Published" : "Upcoming" };
        // Contracts enter the public record only through the explicit lifecycle transition.
        if (kind == "upcoming" && request.Data.Deserialize<UpcomingProfileData>(HubValidation.JsonOptions)!.ContractAddress.Length > 0)
            return BadRequest(new { detail = "Publish the upcoming profile before adding its contract." });
        entry.History.Add(new(entry.Version, entry.UpdatedAt, entry.Stage, entry.Payload));
        await repository.CreateAsync(entry, cancellationToken);
        return Created($"/api/hub/{kind}/{entry.Id}", View(entry));
    }
    [Authorize]
    [HttpPut("{kind}/{id}")]
    [RequestSizeLimit(30000)]
    public async Task<IActionResult> Update(string kind, string id, HubWriteRequest request, CancellationToken cancellationToken)
    {
        HubEntry? entry = await repository.GetAsync(id, cancellationToken);
        if (entry is null || entry.Kind != kind)
            return NotFound();
        if (UserId is null || (entry.OwnerId != UserId && !IsModerator))
            return Forbid();
        if (kind == "applications")
            return BadRequest(new { detail = "Applications cannot be overwritten." });
        if ((kind == "upcoming" && !CanWriteProjects) || (kind == "events" && !CanWriteEvents))
            return Forbid();
        string? error = HubValidation.Validate(kind, request.Data);
        if (error is not null)
            return BadRequest(new { detail = error });
        if (entry.TokenId is not null || entry.Stage == "Trading live")
            return Conflict(new { detail = "The launch is permanent. Update its live token profile instead." });
        if (kind == "upcoming")
        {
            UpcomingProfileData oldData = JsonSerializer.Deserialize<UpcomingProfileData>(entry.Payload, HubValidation.JsonOptions)!;
            UpcomingProfileData newData = request.Data.Deserialize<UpcomingProfileData>(HubValidation.JsonOptions)!;
            if (oldData.Network != newData.Network || oldData.ContractAddress != newData.ContractAddress)
                return BadRequest(new { detail = "Network and contract identity cannot be edited here. Use the contract publication action." });
        }
        entry.Payload = request.Data.GetRawText();
        return await Save(entry, request.Version, cancellationToken);
    }
    [Authorize]
    [RequestSizeLimit(30000)]
    [HttpPost("upcoming/{id}/advance")]
    public async Task<IActionResult> Advance(string id, HubWriteRequest request, CancellationToken cancellationToken)
    {
        HubEntry? entry = await repository.GetAsync(id, cancellationToken);
        if (entry is null || entry.Kind != "upcoming")
            return NotFound();
        if (!CanWriteProjects || UserId is null || (entry.OwnerId != UserId && !IsModerator))
            return Forbid();
        if (request.Version != entry.Version)
            return Conflict(new { detail = "This profile changed. Reload before continuing." });
        if (entry.TokenId is not null)
            return Ok(View(entry));
        string? error = HubValidation.Validate("upcoming", request.Data);
        if (error is not null)
            return BadRequest(new { detail = error });
        UpcomingProfileData data = request.Data.Deserialize<UpcomingProfileData>(HubValidation.JsonOptions)!;
        UpcomingProfileData previous = JsonSerializer.Deserialize<UpcomingProfileData>(entry.Payload, HubValidation.JsonOptions)!;
        if (previous.Network != data.Network || (entry.Stage != "Upcoming" && previous.ContractAddress != data.ContractAddress))
            return BadRequest(new { detail = "Published network and contract identity are permanent." });
        // Lifecycle requests may change only contract/trading evidence, never the pinned story.
        data = previous with { ContractAddress = data.ContractAddress, TradingUrl = data.TradingUrl };
        if (string.IsNullOrWhiteSpace(data.ContractAddress))
            return BadRequest(new { detail = "A contract address is required." });
        if (entry.Stage == "Upcoming")
            entry.Stage = "Contract published";
        else if (entry.Stage == "Contract published")
        {
            if (string.IsNullOrWhiteSpace(data.TradingUrl))
                return BadRequest(new { detail = "A trading link is required." });
            entry.Stage = "Trading live";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(data.TradingUrl))
                return BadRequest(new { detail = "A trading link is required." });
            string tokenId = $"launch-{entry.Id}";
            var token = new TokenProject
            {
                TokenId = tokenId,
                UpcomingProfileId = entry.Id,
                Name = data.Name,
                Symbol = data.Symbol,
                Network = data.Network,
                ContractAddress = data.ContractAddress,
                Description = data.Story,
                Category = "Meme",
                CreatorId = entry.OwnerId,
                LogoUrl = data.LogoUrl,
                WebsiteUrl = data.WebsiteUrl,
                LaunchStatus = LaunchStatus.Published,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                PublishedAt = DateTime.UtcNow,
                SocialLinks = string.IsNullOrEmpty(data.TelegramUrl) ? [] : [new SocialLink { Platform = "Telegram", Url = data.TelegramUrl }],
            };
            bool created = await tokens.TryCreateAsync(token, cancellationToken);
            if (!created)
            {
                TokenProject? existing = await tokens.GetByIdAsync(tokenId, true, cancellationToken);
                if (existing?.CreatorId != entry.OwnerId || existing.ContractAddress != data.ContractAddress)
                    return Conflict(new { detail = "This network and contract already have a token record." });
            }
            entry.TokenId = tokenId;
            entry.Stage = "MTH profile created";
        }
        entry.Payload = JsonSerializer.Serialize(data, HubValidation.JsonOptions);
        return await Save(entry, request.Version, cancellationToken);
    }
    private async Task<IActionResult> Save(HubEntry entry, long version, CancellationToken cancellationToken)
    {
        if (entry.Version != version)
            return Conflict(new { detail = "This record changed. Reload before saving." });
        entry.Version++;
        entry.UpdatedAt = DateTime.UtcNow;
        entry.History.Add(new(entry.Version, entry.UpdatedAt, entry.Stage, entry.Payload));
        return await repository.ReplaceAsync(entry, version, cancellationToken) ? Ok(View(entry)) : Conflict(new { detail = "This record changed. Reload before saving." });
    }
}
