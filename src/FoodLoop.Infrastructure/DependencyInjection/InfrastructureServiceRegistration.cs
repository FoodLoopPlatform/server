using System.Text;
using FoodLoop.Application;
using FoodLoop.Application.Common.Interfaces;
using CloudinaryDotNet;
using FoodLoop.Infrastructure.Features.Auth;
using FoodLoop.Infrastructure.Identity;
using FoodLoop.Infrastructure.Options;
using FoodLoop.Infrastructure.Persistence;
using FoodLoop.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FoodLoop.Infrastructure.DependencyInjection;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, string webRootPath)
    {
        var resolvedWebRoot = string.IsNullOrEmpty(webRootPath) 
            ? Path.Combine(AppContext.BaseDirectory, "wwwroot") 
            : webRootPath;

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ASP.NET Core Identity for password hashing, lockout, role management, tokens, etc.
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.User.RequireUniqueEmail = true;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        // Admin user options
        services.AddOptions<AdminUserOptions>()
            .Bind(configuration.GetSection(AdminUserOptions.SectionName))
            .ValidateDataAnnotations();

        // JWT Bearer authentication
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations();

        // AddJwtBearer's options delegate below runs outside DI, so we still need a plain
        // instance here to build TokenValidationParameters; the AddOptions<> above is what
        // makes JwtOptions validated + injectable everywhere else (e.g. JwtTokenService).
        var jwtSettings = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                };

                // Add support for SignalR query string tokens
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        services.AddHttpContextAccessor();

        // Application service abstractions backed by Infrastructure implementations.
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        
        // Bind SMTP options using the ASP.NET Core Options Pattern
        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .Configure(options =>
            {
                // Fallback configuration support for flat environment/env-file variables
                if (string.IsNullOrEmpty(options.Host))
                {
                    options.Host = configuration["SMTP_HOST"] ?? Environment.GetEnvironmentVariable("SMTP_HOST") ?? string.Empty;
                    options.Username = configuration["SMTP_USERNAME"] ?? Environment.GetEnvironmentVariable("SMTP_USERNAME") ?? string.Empty;
                    options.Password = configuration["SMTP_PASSWORD"] ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD") ?? string.Empty;
                    options.FromEmail = configuration["SMTP_FROM_EMAIL"] ?? Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL") ?? string.Empty;

                    var portStr = configuration["SMTP_PORT"] ?? Environment.GetEnvironmentVariable("SMTP_PORT");
                    if (int.TryParse(portStr, out var port))
                    {
                        options.Port = port;
                    }
                }
            });

        // Conditional Email Service registration (SMTP vs DevStub)
        var smtpHost = configuration["SMTP_HOST"] ?? Environment.GetEnvironmentVariable("SMTP_HOST") ?? configuration["Smtp:Host"];
        if (!string.IsNullOrEmpty(smtpHost))
        {
            services.AddScoped<IEmailService, SmtpEmailService>();
        }
        else
        {
            services.AddScoped<IEmailService, NullEmailService>();
        }
        
        // Bind Cloudinary options using the ASP.NET Core Options Pattern
        services.AddOptions<CloudinaryOptions>()
            .Bind(configuration.GetSection(CloudinaryOptions.SectionName))
            .Configure(options =>
            {
                // Fallback configuration support for flat environment/env-file variables
                if (string.IsNullOrEmpty(options.Url))
                {
                    options.Url = configuration["CLOUDINARY_URL"] ?? Environment.GetEnvironmentVariable("CLOUDINARY_URL") ?? string.Empty;
                }
            });

        // Conditional File Storage Service registration (Cloudinary vs Local disk)
        var cloudinaryUrl = configuration["CLOUDINARY_URL"] ?? Environment.GetEnvironmentVariable("CLOUDINARY_URL") ?? configuration["Cloudinary:Url"];
        if (!string.IsNullOrEmpty(cloudinaryUrl))
        {
            services.AddSingleton(provider =>
            {
                var options = provider.GetRequiredService<IOptions<CloudinaryOptions>>().Value;
                var cloudinary = new Cloudinary(new Account(options.Url));
                cloudinary.Api.Secure = true;
                return cloudinary;
            });
            services.AddScoped<IFileStorageService, CloudinaryFileStorageService>();
        }
        else
        {
            services.AddScoped<IFileStorageService>(_ => new LocalFileStorageService(resolvedWebRoot));
        }

        services.AddScoped<ILocalizationService, LocalizationService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IRealTimeNotificationService, RealTimeNotificationService>();

        // CQRS: commands/queries live in the Application assembly, handlers live here in
        // Infrastructure (they depend on Identity's UserManager<ApplicationUser> and other
        // Infrastructure-only concerns), so MediatR needs to scan both assemblies.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
            AssemblyReference.Assembly,
            typeof(InfrastructureServiceRegistration).Assembly));

        services.AddScoped<IAuthTokenIssuer, AuthTokenIssuer>();

        return services;
    }
}
