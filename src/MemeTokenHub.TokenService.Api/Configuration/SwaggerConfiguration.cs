using Microsoft.OpenApi;

namespace MemeTokenHub.TokenService.Api.Configuration;

public static class SwaggerConfiguration
{
    public static IServiceCollection AddTokenServiceSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "MemeTokenHub Token Service API",
                Version = "v1",
                Description = "Token project publication, discovery, feeds, analytics, and community projections.",
            });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter a MemeTokenHub backend JWT.",
            });
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document, null)] = [],
            });
        });
        return services;
    }
}
