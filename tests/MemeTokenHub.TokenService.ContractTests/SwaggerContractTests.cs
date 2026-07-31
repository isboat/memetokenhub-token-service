using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MemeTokenHub.TokenService.ContractTests;

public sealed class SwaggerContractTests
{
    [Test]
    public async Task SwaggerDocumentContainsRequiredTokenRoutesAndBearerSecurity()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
        using HttpClient client = factory.CreateClient();
        string json = await client.GetStringAsync("/swagger/v1/swagger.json");
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement paths = document.RootElement.GetProperty("paths");
        JsonElement securitySchemes = document.RootElement.GetProperty("components").GetProperty("securitySchemes");

        Assert.Multiple(() =>
        {
            Assert.That(paths.TryGetProperty("/api/tokens", out _), Is.True);
            Assert.That(paths.TryGetProperty("/api/tokens/{tokenId}/publish", out _), Is.True);
            Assert.That(paths.TryGetProperty("/api/tokens/networks", out _), Is.True);
            Assert.That(paths.TryGetProperty("/api/tokens/{tokenId}/sentiment", out _), Is.True);
            Assert.That(securitySchemes.TryGetProperty("Bearer", out _), Is.True);
        });
    }
}
