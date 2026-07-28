using System.Net;
using System.Text.Json;
using FoodLoop.API.Common;
using FoodLoop.Application.Common.Exceptions;
using FoodLoop.Application.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace FoodLoop.API.Middleware;

/// <summary>
/// Converts unhandled exceptions into the standard {success:false, message, errors} envelope
/// (API Documentation section 15) instead of leaking stack traces to clients.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;
    private readonly IConfiguration _config;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env, IConfiguration config)
    {
        _next = next;
        _logger = logger;
        _env = env;
        _config = config;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // ILocalizationService is scoped — resolve from the per-request scope so the
        // correct culture (set by UseRequestLocalization) is used.
        var loc = context.RequestServices.GetService<ILocalizationService>();

        var includeDetails = !_env.IsProduction() || _config.GetValue<bool>("DetailedErrors");

        var (statusCode, message) = exception switch
        {
            NotFoundException => (HttpStatusCode.NotFound, exception.Message),
            ForbiddenAccessException => (HttpStatusCode.Forbidden, exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, loc?["Unauthorized"] ?? "Unauthorized."),
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
            _ => (HttpStatusCode.InternalServerError, includeDetails
                ? $"[{exception.GetType().Name}] {exception.Message} {(exception.InnerException != null ? "Inner: " + exception.InnerException.Message : "")}"
                : (loc?["UnexpectedError"] ?? "An unexpected error occurred.")),
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = ApiResponse.Fail(message);
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
