using MemeTokenHub.TokenService.Api.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace MemeTokenHub.TokenService.Api.Middleware;

public sealed partial class ProblemDetailsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsMiddleware> _logger;

    public ProblemDetailsMiddleware(RequestDelegate next, ILogger<ProblemDetailsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApiException exception)
        {
            await WriteProblemAsync(context, exception.StatusCode, exception.Title, exception.Message);
        }
        catch (Exception exception)
        {
            LogUnhandledError(_logger, exception, context.TraceIdentifier);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "Unexpected error", "An unexpected error occurred.");
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled error while processing request {TraceIdentifier}")]
    private static partial void LogUnhandledError(ILogger logger, Exception exception, string traceIdentifier);

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string title, string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
            Extensions = { ["traceId"] = context.TraceIdentifier },
        };
        await context.Response.WriteAsJsonAsync(problem);
    }
}
