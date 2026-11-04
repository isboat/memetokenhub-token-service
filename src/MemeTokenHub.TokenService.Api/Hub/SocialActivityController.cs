using MemeTokenHub.TokenService.Api.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
namespace MemeTokenHub.TokenService.Api.Hub;

[ApiController]
[Route("api/hub/activity")]
public sealed class SocialActivityController(IMongoClient client, IOptions<MongoDbOptions> options) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<SocialChange>> Get(CancellationToken cancellationToken) =>
        await client.GetDatabase(options.Value.DatabaseName).GetCollection<SocialChange>("HubSocialChanges")
            .Find(Builders<SocialChange>.Filter.Empty).SortByDescending(x => x.ObservedAt).Limit(200).ToListAsync(cancellationToken);
}
