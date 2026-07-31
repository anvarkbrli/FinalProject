using System.Globalization;
using System.Security.Claims;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Serialization;
using Ludora.API.BackgroundServices;
using Ludora.API.Filters;
using Ludora.API.Hubs;
using Ludora.API.Localization;
using Ludora.API.Middlewares;
using Ludora.API.Security;
using Ludora.Application.Common;
using Ludora.Application.Interfaces;
using Ludora.Application.Settings;
using Ludora.Domain.Entities.User;
using Ludora.Infrastructure.Email;
using Ludora.Infrastructure.Caching;
using Ludora.Infrastructure.FpsResearch;
using Ludora.Infrastructure.IGDB;
using Ludora.Infrastructure.Steam;
using Ludora.Infrastructure.Translation;
using Ludora.Persistence.Data;
using Ludora.Persistence.ServiceRegistration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace Ludora.API;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
        builder.Services.Configure<RequestLocalizationOptions>(options =>
        {
            var supportedCultures = new[]
            {
                new CultureInfo("az-AZ"),
                new CultureInfo("en-US")
            };

            options.DefaultRequestCulture = new RequestCulture("az-AZ");
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;
            options.RequestCultureProviders =
            [
                new AcceptLanguageHeaderRequestCultureProvider()
            ];
        });
        builder.Services.AddScoped<AdminActionAuditFilter>();
        builder.Services
            .AddControllers(options =>
                options.Filters.AddService<AdminActionAuditFilter>())
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter()))
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(entry => entry.Value?.Errors.Count > 0)
                        .ToDictionary(
                            entry => entry.Key,
                            entry => entry.Value!.Errors
                                .Select(GetModelStateErrorMessage)
                                .ToArray());

                    return new BadRequestObjectResult(new
                    {
                        statusCode = StatusCodes.Status400BadRequest,
                        message = "Göndərilən məlumatlarda səhv var. Zəhmət olmasa sahələri yoxlayın.",
                        errors,
                        traceId = context.HttpContext.TraceIdentifier
                    });
                };
            });
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });
        builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
            options.Level = CompressionLevel.Fastest);
        builder.Services.Configure<GzipCompressionProviderOptions>(options =>
            options.Level = CompressionLevel.Fastest);
        builder.Services
            .AddSignalR(options =>
            {
                options.EnableDetailedErrors = builder.Environment.IsDevelopment();
            })
            .AddJsonProtocol(options =>
                options.PayloadSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter()));
        builder.Services.AddHostedService<NotificationOutboxWorker>();
        builder.Services.AddHostedService<EmailOutboxWorker>();
        builder.Services.AddSingleton<IUserPresenceTracker, UserPresenceTracker>();
        builder.Services.AddSingleton<IBadgeEvaluationQueue, BadgeEvaluationQueue>();
        builder.Services.AddSingleton<ISteamSyncQueue, SteamSyncQueue>();
        builder.Services.AddHostedService<BadgeEvaluationWorker>();
        builder.Services.AddHostedService<SteamSyncWorker>();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT access tokenini daxil edin."
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                }] = Array.Empty<string>()
            });
        });

        builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

        var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()
            ?? throw new InvalidOperationException("JwtSettings konfiqurasiyası tapılmadı.");
        if (string.IsNullOrWhiteSpace(jwtSettings.Key) || jwtSettings.Key.Length < 32)
            throw new InvalidOperationException("JwtSettings:Key user-secrets və ya environment variable vasitəsilə ən azı 32 simvol verilməlidir.");

        var fileStorageSettings = builder.Configuration.GetSection("FileStorageSettings").Get<FileStorageSettings>()
            ?? new FileStorageSettings();
        builder.Services.AddSingleton(fileStorageSettings);

        var malwareSettings = builder.Configuration
            .GetSection("MalwareScanning")
            .Get<MalwareScanningSettings>() ?? new MalwareScanningSettings();

        if (!builder.Environment.IsDevelopment() && !malwareSettings.Enabled)
        {
            throw new InvalidOperationException(
                "Production mühitində MalwareScanning:Enabled true olmalıdır.");
        }

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;

                    if (!string.IsNullOrWhiteSpace(accessToken) &&
                        (path.StartsWithSegments("/hubs/notifications") ||
                         path.StartsWithSegments("/hubs/chat")))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    var userIdValue = context.Principal?
                        .FindFirstValue(ClaimTypes.NameIdentifier);

                    if (!Guid.TryParse(userIdValue, out var userId))
                    {
                        context.Fail("Token istifadəçisi müəyyən edilmədi.");
                        return;
                    }

                    var userManager = context.HttpContext.RequestServices
                        .GetRequiredService<UserManager<AppUser>>();

                    var user = await userManager.FindByIdAsync(userId.ToString());

                    if (user is null)
                    {
                        context.Fail("İstifadəçi tapılmadı.");
                        return;
                    }

                    var tokenSecurityStamp = context.Principal?
                        .FindFirstValue("security_stamp");

                    if (!string.Equals(
                            tokenSecurityStamp,
                            user.SecurityStamp ?? string.Empty,
                            StringComparison.Ordinal))
                    {
                        context.Fail("Token artıq etibarlı deyil. Yenidən daxil olun.");
                        return;
                    }

                    var sessionIdValue = context.Principal?
                        .FindFirstValue("session_id");

                    if (!Guid.TryParse(sessionIdValue, out var sessionId))
                    {
                        context.Fail("Token cihaz sessiyasına bağlı deyil. Yenidən daxil olun.");
                        return;
                    }

                    var now = DateTime.UtcNow;
                    var dbContext = context.HttpContext.RequestServices
                        .GetRequiredService<AppDbContext>();
                    var sessionIsActive = await dbContext.RefreshSessions
                        .AsNoTracking()
                        .AnyAsync(session =>
                            session.Id == sessionId &&
                            session.UserId == userId &&
                            session.SecurityStamp == (user.SecurityStamp ?? string.Empty) &&
                            session.RevokedAtUtc == null &&
                            session.ExpiresAtUtc > now,
                            context.HttpContext.RequestAborted);

                    if (!sessionIsActive)
                    {
                        context.Fail("Bu cihaz sessiyası ləğv edilib. Yenidən daxil olun.");
                        return;
                    }

                    var isBanned = user.IsPermanentlyBanned ||
                                   (user.BannedUntilUtc.HasValue &&
                                    user.BannedUntilUtc > now);

                    if (!user.EmailConfirmed)
                    {
                        context.Fail("Hesabın email ünvanı təsdiqlənməyib.");
                        return;
                    }

                    if (user.AccountDeletionRequestedAtUtc.HasValue)
                    {
                        context.Fail("Hesabın silinməsi başladılıb.");
                        return;
                    }

                    if (isBanned)
                        context.Fail("Hesab admin tərəfindən banlanıb.");
                },
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    if (context.Response.HasStarted)
                        return;

                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json; charset=utf-8";

                    var failureMessage = context.AuthenticateFailure?.Message;
                    var message = failureMessage switch
                    {
                        "Token artıq etibarlı deyil. Yenidən daxil olun." => failureMessage,
                        "Bu cihaz sessiyası ləğv edilib. Yenidən daxil olun." => failureMessage,
                        "Hesabın email ünvanı təsdiqlənməyib." => failureMessage,
                        "Hesabın silinməsi başladılıb." => failureMessage,
                        "Hesab admin tərəfindən banlanıb." => failureMessage,
                        _ when context.AuthenticateFailure is SecurityTokenExpiredException =>
                            "Sessiyanın vaxtı bitib. Yenidən daxil olun.",
                        _ => "Sessiya etibarlı deyil. Yenidən daxil olun."
                    };

                    await context.Response.WriteAsJsonAsync(new
                    {
                        statusCode = StatusCodes.Status401Unauthorized,
                        message,
                        traceId = context.HttpContext.TraceIdentifier
                    });
                },
                OnForbidden = async context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        statusCode = StatusCodes.Status403Forbidden,
                        message = "Bu əməliyyat üçün icazəniz yoxdur.",
                        traceId = context.HttpContext.TraceIdentifier
                    });
                }
            };
        });

        builder.Services.AddAuthorization(options =>
        {
            foreach (var capability in CapabilityNames.All)
            {
                var allowedRoles = RoleCapabilityMatrix
                    .GetRolesWithCapability(capability)
                    .ToArray();

                options.AddPolicy(
                    capability,
                    policy => policy.RequireRole(allowedRoles));
            }
        });
        builder.Services.AddWellPlaydRateLimiting();

        builder.Services.AddCachingInfrastructure();
        builder.Services.AddPersistenceServices(builder.Configuration);
        builder.Services.AddEmailInfrastructure(builder.Configuration);
        builder.Services.AddIgdbIntegration(builder.Configuration);
        builder.Services.AddFpsResearchIntegration(builder.Configuration);
        builder.Services.AddSteamIntegration(builder.Configuration);
        builder.Services.AddTranslationInfrastructure(builder.Configuration);

        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
        {
            if (origins.Length == 0)
                throw new InvalidOperationException("Cors:AllowedOrigins boş ola bilməz.");
            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }));

        var app = builder.Build();
        await EnsureRolesAsync(app.Services);

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseRequestLocalization();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<GlobalExceptionMiddleware>();
        app.UseResponseCompression();

        if (!app.Environment.IsDevelopment())
            app.UseHsts();

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseCors("Frontend");
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHub<NotificationHub>("/hubs/notifications");
        app.MapHub<ChatHub>("/hubs/chat");
        await app.RunAsync();
    }


    private static string GetModelStateErrorMessage(Microsoft.AspNetCore.Mvc.ModelBinding.ModelError error)
    {
        var message = error.ErrorMessage?.Trim();
        if (string.IsNullOrWhiteSpace(message))
            return "Dəyər düzgün formatda deyil.";

        if (message.StartsWith("The JSON value could not be converted", StringComparison.OrdinalIgnoreCase) ||
            message.StartsWith("The input was not valid", StringComparison.OrdinalIgnoreCase) ||
            message.StartsWith("A possible object cycle was detected", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Path:", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("LineNumber:", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("BytePositionInLine:", StringComparison.OrdinalIgnoreCase))
        {
            return "Dəyər düzgün formatda deyil.";
        }

        return message;
    }

    private static async Task EnsureRolesAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();

        var roleManager =
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        string[] roleNames =
        [
            RoleNames.Admin,
            RoleNames.Moderator,
            RoleNames.User
        ];

        foreach (var roleName in roleNames)
        {
            if (await roleManager.RoleExistsAsync(roleName))
                continue;

            var result =
                await roleManager.CreateAsync(
                    new IdentityRole<Guid>(roleName));

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"{roleName} rolu yaradıla bilmədi: " +
                    string.Join(
                        ", ",
                        result.Errors.Select(x => x.Description)));
            }
        }
    }
}
