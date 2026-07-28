using System.Security.Claims;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Ludora.API.BackgroundServices;
using Ludora.API.Hubs;
using Ludora.API.Middlewares;
using Ludora.Application.Common;
using Ludora.Application.Interfaces;
using Ludora.Application.Settings;
using Ludora.Domain.Entities.User;
using Ludora.Infrastructure.Email;
using Ludora.Infrastructure.FpsResearch;
using Ludora.Infrastructure.IGDB;
using Ludora.Infrastructure.Steam;
using Ludora.Persistence.ServiceRegistration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace Ludora.API;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers().AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
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

                    var now = DateTime.UtcNow;
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
                }
            };
        });

        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter =
                PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    if (!context.Request.Path.StartsWithSegments(
                            "/api/fps-estimator"))
                    {
                        return RateLimitPartition.GetNoLimiter(
                            "non-fps-endpoint");
                    }

                    var ipAddress =
                        context.Connection.RemoteIpAddress?.ToString() ??
                        "unknown";

                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"fps-ip:{ipAddress}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 20,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        });
                });
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType =
                    "application/json; charset=utf-8";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        message =
                            "Çox sayda sorğu göndərmisiniz. Bir qədər sonra yenidən cəhd edin."
                    },
                    cancellationToken);
            };
            options.AddPolicy("Auth", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
            options.AddPolicy("UserReports", context =>
            {
                var partitionKey =
                    context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                    context.Connection.RemoteIpAddress?.ToString() ??
                    "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    $"user-report:{partitionKey}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
            options.AddPolicy("FpsEstimator", context =>
            {
                var userId =
                    context.User.FindFirstValue(
                        ClaimTypes.NameIdentifier) ??
                    "anonymous";

                return RateLimitPartition.GetFixedWindowLimiter(
                    $"fps-user:{userId}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
            options.AddPolicy("SteamSync", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                context.Connection.RemoteIpAddress?.ToString() ??
                "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
            options.AddPolicy("Chat", context => RateLimitPartition.GetTokenBucketLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                context.Connection.RemoteIpAddress?.ToString() ??
                "unknown",
                _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 30,
                    TokensPerPeriod = 15,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        });

        builder.Services.AddPersistenceServices(builder.Configuration);
        builder.Services.AddEmailInfrastructure(builder.Configuration);
        builder.Services.AddIgdbIntegration(builder.Configuration);
        builder.Services.AddFpsResearchIntegration(builder.Configuration);
        builder.Services.AddSteamIntegration(builder.Configuration);

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

        app.UseMiddleware<GlobalExceptionMiddleware>();
        app.UseResponseCompression();
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
