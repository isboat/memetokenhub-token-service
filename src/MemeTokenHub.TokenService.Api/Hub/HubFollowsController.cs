using System.Security.Claims;
using MemeTokenHub.TokenService.Api.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
namespace MemeTokenHub.TokenService.Api.Hub;

[ApiController]
[Authorize]
[Route("api/hub/upcoming/{id}/follow")]
public sealed class HubFollowsController(IMongoClient mongo, IOptions<MongoDbOptions> options, IHubRepository entries) : ControllerBase
{
    private IMongoCollection<HubFollow> Follows => mongo.GetDatabase(options.Value.DatabaseName).GetCollection<HubFollow>("HubFollows");
    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private string Key(string id) => $"{UserId}:{id}";
    [HttpGet]
    public async Task<IActionResult> Get(string id, CancellationToken cancellationToken)
    {
        if (UserId is null)
            return Unauthorized();
        return Ok(new { following = await Follows.Find(x => x.Id == Key(id)).AnyAsync(cancellationToken) });
    }
    [HttpPut]
    public async Task<IActionResult> Follow(string id, CancellationToken cancellationToken)
    {
        if (UserId is null)
            return Unauthorized();
        HubEntry? entry = await entries.GetAsync(id, cancellationToken);
        if (entry?.Kind != "upcoming")
            return NotFound();
        await Follows.ReplaceOneAsync(x => x.Id == Key(id), new HubFollow { Id = Key(id), UserId = UserId, ProjectId = id }, new ReplaceOptions { IsUpsert = true }, cancellationToken);
        return Ok(new { following = true });
    }
    [HttpDelete]
    public async Task<IActionResult> Unfollow(string id, CancellationToken cancellationToken)
    {
        if (UserId is null)
            return Unauthorized();
        await Follows.DeleteOneAsync(x => x.Id == Key(id), cancellationToken);
        return Ok(new { following = false });
    }
}
