using System.Text;
using MemeTokenHub.TokenService.Api.Application.Abstractions;
using MemeTokenHub.TokenService.Api.Application.Services;
using MemeTokenHub.TokenService.Api.Configuration;
using MemeTokenHub.TokenService.Api.Hub;
using MemeTokenHub.TokenService.Api.Infrastructure;
using MemeTokenHub.TokenService.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
IHealthChecksBuilder healthChecks = builder.Services.AddHealthChecks();
if (!builder.Environment.IsEnvironment("Testing"))
{
    healthChecks.AddCheck<MongoDbHealthCheck>("mongodb", tags: ["ready"]);
}
builder.Services.AddTokenServiceSwagger();
builder.Services.AddOptions<MediaStorageOptions>()
    .Bind(builder.Configuration.GetSection(MediaStorageOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.UploadBaseUrl), "Media upload base URL is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.AssetBaseUrl), "Media asset base URL is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.SigningKey), "Media signing key is required.")
    .ValidateOnStart();
builder.Services.AddOptions<MongoDbOptions>()
    .Bind(builder.Configuration.GetSection(MongoDbOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "MongoDB connection string is required.")
    .ValidateOnStart();

string jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");
string jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is required.");
string jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is required.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ProjectsWrite", policy => policy.RequireAssertion(context =>
        context.User.HasClaim("capability", "projects:write") || context.User.IsInRole("Moderator")));
});

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(builder.Configuration["MongoDb:ConnectionString"]));
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ITokenRepository, MongoTokenRepository>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IHubRepository, MongoHubRepository>();
builder.Services.AddHttpClient("HubSources", client => client.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddHostedService<SocialActivityCollector>();
builder.Services.AddScoped<IMediaUploadService, MediaUploadService>();
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<MongoIndexInitializer>();
}

WebApplication app = builder.Build();
app.UseMiddleware<ProblemDetailsMiddleware>();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "MemeTokenHub Token Service v1");
    options.RoutePrefix = "swagger";
    options.DisplayRequestDuration();
});
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.Run();

public partial class Program;
