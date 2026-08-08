using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;

namespace FoodLoop.API.Swagger;

/// <summary>
/// Injects a global Accept-Language header parameter into every Swagger operation.
/// Defaults to "ar" (the application default); callers can switch to "en" as needed.
/// </summary>
public class AcceptLanguageHeaderFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "Accept-Language",
            In = ParameterLocation.Header,
            Required = false,
            Schema = new OpenApiSchema
            {
                Type = "string",
                Enum = new List<Microsoft.OpenApi.Any.IOpenApiAny>
                {
                    new Microsoft.OpenApi.Any.OpenApiString("ar"),
                    new Microsoft.OpenApi.Any.OpenApiString("en"),
                },
                Default = new Microsoft.OpenApi.Any.OpenApiString("ar")
            },
            Description = "Response language. Supported: ar (default), en."
        });
    }
}
