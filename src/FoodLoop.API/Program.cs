using FoodLoop.Infrastructure.DependencyInjection;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Hubs;
using FoodLoop.API.Middleware;
using FoodLoop.API.Options;
using FoodLoop.API.Swagger;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Globalization;
using Serilog;

// Load environment variables from local .env file if present in working dir, AppContext, or API folder
var candidateEnvPaths = new[]
{
    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
    Path.Combine(Directory.GetCurrentDirectory(), "src", "FoodLoop.API", ".env"),
    Path.Combine(AppContext.BaseDirectory, ".env")
};

foreach (var envPath in candidateEnvPaths)
{
    if (File.Exists(envPath))
    {
        foreach (var line in File.ReadAllLines(envPath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                continue;

            var parts = trimmed.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var val = parts[1].Trim();
                if (val.StartsWith("\"") && val.EndsWith("\""))
                    val = val[1..^1];
                else if (val.StartsWith("'") && val.EndsWith("'"))
                    val = val[1..^1];

                Environment.SetEnvironmentVariable(key, val);
            }
        }
        break;
    }
}

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(AppContext.BaseDirectory, "logs/log-.txt"),
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddHealthChecks();

// ---- Localization -------------------------------------------------------

// IStringLocalizerFactory is registered here; LocalizationService uses factory.Create()
// with an explicit assembly reference so resource files in FoodLoop.Infrastructure are found
// regardless of where AddLocalization() is called from.
builder.Services.AddLocalization();

var supportedCultures = new[] { new CultureInfo("ar"), new CultureInfo("en") };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    // UICulture drives string resource lookup (message translations).
    // Culture drives number/date parsing — keep it InvariantCulture so decimal
    // form fields like "6.00" always parse correctly regardless of Accept-Language.
    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(
        culture: CultureInfo.InvariantCulture,
        uiCulture: new CultureInfo("ar"));
    options.SupportedCultures = new[] { CultureInfo.InvariantCulture };
    options.SupportedUICultures = supportedCultures;
    // Resolution order: Accept-Language header → default "ar"
    options.ApplyCurrentCultureToResponseHeaders = true;
    options.RequestCultureProviders.Insert(0, new Microsoft.AspNetCore.Localization.CustomRequestCultureProvider(ctx =>
    {
        // Map Accept-Language to UICulture only; always use InvariantCulture for parsing.
        var acceptLang = ctx.Request.Headers["Accept-Language"].ToString();
        var uiCulture = acceptLang.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? new CultureInfo("en")
            : new CultureInfo("ar");
        var result = new Microsoft.AspNetCore.Localization.ProviderCultureResult(
            CultureInfo.InvariantCulture.Name,
            uiCulture.Name);
        return Task.FromResult<Microsoft.AspNetCore.Localization.ProviderCultureResult?>(result);
    }));
});

// ---- Services ----------------------------------------------------------

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Enums (AddressType, StoreType, VerificationStatus, etc.) go over the wire as strings
        // ("StoreOwner", not 1) so the request/response bodies match what the UI sends.
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddSignalR();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "FoodLoop API", Version = "v1" });

    // Accept-Language header — lets callers switch between "ar" (default) and "en"
    // directly from the Swagger UI "Try it out" panel.
    options.OperationFilter<AcceptLanguageHeaderFilter>();

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

builder.Services.AddOptions<CorsOptions>()
    .Bind(builder.Configuration.GetSection(CorsOptions.SectionName));

// AddCors' policy delegate below runs outside DI, so we still need a plain instance here;
// the AddOptions<> above is what makes CorsOptions injectable anywhere else it's needed.
var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        var origins = corsOptions.AllowedOrigins;
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
            // No origins configured — fall back to allowing any origin without credentials
            // (AllowAnyOrigin + AllowCredentials is rejected by ASP.NET Core and produces
            // no CORS header at all, which is worse than a permissive fallback).
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

// ---- Middleware pipeline ------------------------------------------------

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseRequestLocalization();

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
app.MapHub<NotificationHub>("/hubs/notifications");

// ---- Startup tasks: apply migrations + seed RBAC roles ------------------

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    
    // 1. Critical: Apply Database Migrations
    try
    {
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        Log.Information("Applying database migrations...");
        await dbContext.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "An error occurred during database migration. Host will terminate.");
        throw;
    }

    // 2. Non-Critical: Run Seeder
    try
    {
        Log.Information("Seeding database values...");
        await IdentitySeeder.SeedAsync(services);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred during database seeding. Host will continue running.");
    }
}

try
{
    Log.Information("Starting web host");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
