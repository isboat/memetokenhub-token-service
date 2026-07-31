# MemeTokenHub Token Service

ASP.NET Core and MongoDB microservice that owns token project metadata, publication, discovery feeds, and local community projections.

## Local development

1. Install the .NET 10 SDK and MongoDB.
2. Set `MongoDb__ConnectionString`, `Jwt__Key`, `Jwt__Issuer`, and `Jwt__Audience`.
3. Run `dotnet restore`, `dotnet build --no-restore`, then `dotnet run --project src/MemeTokenHub.TokenService.Api`.
4. Open `/swagger` for the interactive API documentation.

The private `MemeTokenHub.Shared` package feed is registered in `NuGet.config`. Authenticate with a GitHub Packages token before adding or updating the centrally pinned shared package.
