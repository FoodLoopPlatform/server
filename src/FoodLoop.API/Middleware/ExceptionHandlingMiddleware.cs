using System.Net;
using System.Text.Json;
using FoodLoop.API.Common;
using FoodLoop.Application.Common.Exceptions;

namespace FoodLoop.API.Middleware;

/// <summary>
/// Converts unhandled exceptions into the standard {success:false, message, errors} envelope
/// (API Documentation section 15) instead of leaking stack traces to clients.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            NotFoundException => (HttpStatusCode.NotFound, exception.Message),
            ForbiddenAccessException => (HttpStatusCode.Forbidden, exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized."),
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred."),
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
