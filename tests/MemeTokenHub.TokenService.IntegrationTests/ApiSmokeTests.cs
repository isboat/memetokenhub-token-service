using System.Net;

namespace MemeTokenHub.TokenService.IntegrationTests;

public sealed class ApiSmokeTests
{
    [TestCase("/health/live")]
    [TestCase("/health/ready")]
    [TestCase("/swagger/index.html")]
    public async Task PublicInfrastructureEndpointReturnsSuccess(string endpoint)
    {
        await using var factory = new TokenApiFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(endpoint);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task CreateTokenWithoutAuthenticationReturnsUnauthorized()
    {
        await using var factory = new TokenApiFactory();
        using HttpClient client = factory.CreateClient();
        using var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PostAsync("/api/tokens", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
