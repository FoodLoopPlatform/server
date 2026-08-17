using System;
using FoodLoop.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace FoodLoop.Infrastructure.Services;

public class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string CorrelationIdHeaderKey = "X-Correlation-ID";

    public CorrelationIdAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetCorrelationId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return Guid.NewGuid().ToString();
        }

        // 1. Try retrieve from Request Headers
        if (httpContext.Request.Headers.TryGetValue(CorrelationIdHeaderKey, out var headerVal))
        {
            var id = headerVal.ToString();
            if (!string.IsNullOrWhiteSpace(id))
            {
                return id;
            }
        }

        // 2. Try retrieve from HttpContext.Items to preserve the same ID within the current request lifetime
        if (httpContext.Items.TryGetValue(CorrelationIdHeaderKey, out var itemVal) && itemVal is string savedId)
        {
            return savedId;
        }

        // 3. Fallback to generating a new ID and store it in HttpContext.Items
        var newId = Guid.NewGuid().ToString();
        httpContext.Items[CorrelationIdHeaderKey] = newId;
        
        // Also ensure it is attached back to response headers so client/caller can trace it
        httpContext.Response.Headers[CorrelationIdHeaderKey] = newId;

        return newId;
    }
}
