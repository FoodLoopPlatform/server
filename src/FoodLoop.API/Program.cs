using FoodLoop.Infrastructure.DependencyInjection;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.API.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

// ---- Services ----------------------------------------------------------

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Enums (AccountType, AddressType, StoreType, etc.) go over the wire as strings
        // ("StoreOwner", not 1) so the request/response bodies match what the UI sends.
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "FoodLoop API", Version = "v1" });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Scheme = "bearer",
        BearerFormat = "JWT",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Description = "Enter a valid JWT access token (without the 'Bearer ' prefix).",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    options.AddSecurityDefinition("Bearer", jwtScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { { jwtScheme, Array.Empty<string>() } });
});

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.WebRootPath);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else if (origins.Length > 0)
        {
            policy.WithOrigins(origins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

// ---- Middleware pipeline ------------------------------------------------

app.UseMiddleware<ExceptionHandlingMiddleware>();

// if (app.Environment.IsDevelopment())
// {
    app.UseSwagger();
    app.UseSwaggerUI();
// }

app.UseHttpsRedirection();

app.UseStaticFiles(); // serves /uploads/** (see LocalFileStorageService)

app.UseCors("Default");

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () =>
{
    return Results.Ok(new
    {
        message = "Welcome to the FoodLoop API!",
        version = "v1",
        documentation = "/swagger",
        health = "/health"
    });
});

app.MapHealthChecks("/health");

app.MapControllers();

// ---- Startup tasks: apply migrations + seed RBAC roles ------------------

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<ApplicationDbContext>();

    if (app.Environment.IsDevelopment())
    {
        await dbContext.Database.MigrateAsync();
    }

    await IdentitySeeder.SeedRolesAsync(services);
}

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
