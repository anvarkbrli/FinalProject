using System.Globalization;
using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;

using Ludora.API.BackgroundServices;
using Ludora.API.Configuration;
using Ludora.API.Filters;
using Ludora.API.HealthChecks;
using Ludora.API.Hubs;
using Ludora.API.Localization;
using Ludora.API.Middlewares;
using Ludora.API.Security;
using Ludora.Application.Common;
using Ludora.Application.Interfaces;
using Ludora.Application.Settings;
using Ludora.Domain.Entities.User;
using Ludora.Infrastructure.Caching;
using Ludora.Infrastructure.Email;
using Ludora.Infrastructure.FpsResearch;
using Ludora.Infrastructure.IGDB;
using Ludora.Infrastructure.Steam;
using Ludora.Infrastructure.Translation;
using Ludora.Persistence.Data;
using Ludora.Persistence.ServiceRegistration;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

using StackExchange.Redis;

namespace Ludora.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            if (!builder.Environment.IsDevelopment())
            {
                builder.Logging.ClearProviders();
                builder.Logging.AddJsonConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.UseUtcTimestamp = true;
                    options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
                });
            }

            ProductionConfigurationValidator.Validate(
                builder.Configuration,
                builder.Environment);
            var forwardedHeadersSettings = builder.Services
                .AddDeploymentConfiguration(builder.Configuration, builder.Environment);

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
            var cacheSettings = builder.Configuration
                .GetSection(CacheSettings.SectionName)
                .Get<CacheSettings>() ?? new CacheSettings();

            var sqlIdleModeSettings = builder.Configuration
                .GetSection(SqlIdleModeSettings.SectionName)
                .Get<SqlIdleModeSettings>() ?? new SqlIdleModeSettings();
            builder.Services.AddSingleton(sqlIdleModeSettings);

            builder.Services
                .AddOptions<PresenceSettings>()
                .Bind(builder.Configuration.GetSection(PresenceSettings.SectionName))
                .Validate(
                    settings => settings.HeartbeatSeconds is >= 5 and <= 60,
                    "Presence:HeartbeatSeconds 5-60 aralığında olmalıdır.")
                .Validate(
                    settings => settings.OfflineAfterSeconds is >= 30 and <= 600,
                    "Presence:OfflineAfterSeconds 30-600 aralığında olmalıdır.")
                .Validate(
                    settings => settings.OfflineAfterSeconds >= settings.HeartbeatSeconds * 2,
                    "Presence:OfflineAfterSeconds heartbeat intervalının ən azı iki qatı olmalıdır.")
                .Validate(
                    settings => !settings.UseDistributedStore || cacheSettings.RedisEnabled,
                    "Distributed presence üçün Cache:RedisEnabled true olmalıdır.")
                .ValidateOnStart();

            var signalRBuilder = builder.Services
                .AddSignalR(options =>
                {
                    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
                })
                .AddJsonProtocol(options =>
                    options.PayloadSerializerOptions.Converters.Add(
                        new JsonStringEnumConverter()));

            if (cacheSettings.RedisEnabled)
            {
                signalRBuilder.AddStackExchangeRedis(
                    cacheSettings.RedisConnectionString,
                    options =>
                    {
                        options.Configuration.ChannelPrefix = RedisChannel.Literal(
                            $"{cacheSettings.InstanceName}signalr");
                    });
            }

            if (!sqlIdleModeSettings.Enabled)
            {
                builder.Services.AddHostedService<NotificationOutboxWorker>();
                builder.Services.AddHostedService<EmailOutboxWorker>();
            }

            var presenceSettings = builder.Configuration
                .GetSection(PresenceSettings.SectionName)
                .Get<PresenceSettings>() ?? new PresenceSettings();
            if (presenceSettings.UseDistributedStore && cacheSettings.RedisEnabled)
            {
                builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
                    ConnectionMultiplexer.Connect(cacheSettings.RedisConnectionString));
                builder.Services.AddSingleton<RedisUserPresenceTracker>();
                builder.Services.AddSingleton<IUserPresenceTracker>(serviceProvider =>
                    serviceProvider.GetRequiredService<RedisUserPresenceTracker>());
                builder.Services.AddHostedService<PresenceHeartbeatWorker>();
            }
            else
            {
                builder.Services.AddSingleton<IUserPresenceTracker, UserPresenceTracker>();
            }

            builder.Services.AddScoped<UserPresenceCoordinator>();
            builder.Services.AddSingleton<IBadgeEvaluationQueue, BadgeEvaluationQueue>();
            builder.Services.AddSingleton<ISteamSyncQueue, SteamSyncQueue>();

            if (!sqlIdleModeSettings.Enabled)
            {
                builder.Services.AddHostedService<BadgeEvaluationWorker>();
                builder.Services.AddHostedService<SteamSyncWorker>();
                builder.Services.AddHostedService<BackgroundJobCleanupWorker>();
            }

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

            var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
                ?? throw new InvalidOperationException("JwtSettings konfiqurasiyası tapılmadı.");
            if (string.IsNullOrWhiteSpace(jwtSettings.Key) || jwtSettings.Key.Length < 32)
                throw new InvalidOperationException("JwtSettings:Key user-secrets və ya environment variable vasitəsilə ən azı 32 simvol verilməlidir.");

            builder.Services
                .AddOptions<DemoDeploymentSettings>()
                .Bind(builder.Configuration.GetSection(DemoDeploymentSettings.SectionName));

            var demoDeploymentSettings = builder.Configuration
                .GetSection(DemoDeploymentSettings.SectionName)
                .Get<DemoDeploymentSettings>() ?? new DemoDeploymentSettings();

            var malwareSettings = builder.Configuration
                .GetSection("MalwareScanning")
                .Get<MalwareScanningSettings>() ?? new MalwareScanningSettings();

            if (!builder.Environment.IsDevelopment() &&
                !demoDeploymentSettings.Enabled &&
                !malwareSettings.Enabled)
            {
                throw new InvalidOperationException(
                    "Production mühitində MalwareScanning:Enabled true olmalıdır. " +
                    "Yalnız təqdimat quruluşunda DemoDeployment:Enabled true olduqda deaktiv edilə bilər.");
            }

            var googleSettings = builder.Configuration
                .GetSection(GoogleAuthenticationSettings.SectionName)
                .Get<GoogleAuthenticationSettings>() ?? new GoogleAuthenticationSettings();
            builder.Services
                .AddOptions<GoogleAuthenticationSettings>()
                .Bind(builder.Configuration.GetSection(GoogleAuthenticationSettings.SectionName))
                .Validate(settings => settings.TicketLifetimeMinutes is >= 3 and <= 30,
                    "Authentication:Google:TicketLifetimeMinutes 3-30 aralığında olmalıdır.")
                .Validate(settings => !settings.Enabled ||
                                      (!string.IsNullOrWhiteSpace(settings.ClientId) &&
                                       !string.IsNullOrWhiteSpace(settings.ClientSecret)),
                    "Google authentication aktiv olduqda ClientId və ClientSecret verilməlidir.")
                .Validate(settings => !settings.Enabled ||
                                      (!string.IsNullOrWhiteSpace(settings.CallbackPath) &&
                                       settings.CallbackPath.StartsWith("/", StringComparison.Ordinal) &&
                                       !settings.CallbackPath.StartsWith("//", StringComparison.Ordinal)),
                    "Authentication:Google:CallbackPath düzgün relative path olmalıdır.")
                .Validate(settings => !settings.Enabled ||
                                      (Uri.TryCreate(settings.FrontendCallbackUrl, UriKind.Absolute, out var callbackUri) &&
                                       callbackUri.Scheme is "http" or "https"),
                    "Authentication:Google:FrontendCallbackUrl absolute HTTP/HTTPS URL olmalıdır.")
                .ValidateOnStart();

            var authenticationBuilder = builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            });
            authenticationBuilder.AddJwtBearer(options =>
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

            authenticationBuilder.AddCookie(IdentityConstants.ExternalScheme, options =>
            {
                options.Cookie.Name = ".WellPlayd.External";
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
                options.SlidingExpiration = false;
            });

            if (googleSettings.Enabled)
            {
                authenticationBuilder.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
                {
                    options.ClientId = googleSettings.ClientId;
                    options.ClientSecret = googleSettings.ClientSecret;
                    options.SignInScheme = IdentityConstants.ExternalScheme;
                    options.CallbackPath = googleSettings.CallbackPath;
                    options.SaveTokens = false;
                    options.Scope.Clear();
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("email");
                    options.CorrelationCookie.HttpOnly = true;
                    options.CorrelationCookie.IsEssential = true;
                    options.CorrelationCookie.SameSite = builder.Environment.IsDevelopment()
                        ? SameSiteMode.Lax
                        : SameSiteMode.None;
                    options.CorrelationCookie.SecurePolicy = builder.Environment.IsDevelopment()
                        ? CookieSecurePolicy.SameAsRequest
                        : CookieSecurePolicy.Always;
                    options.Events.OnCreatingTicket = context =>
                    {
                        var verified =
                            context.User.TryGetProperty("verified_email", out var verifiedEmail) &&
                            verifiedEmail.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False
                                ? verifiedEmail.GetBoolean()
                                : context.User.TryGetProperty("email_verified", out var emailVerified) &&
                                  emailVerified.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False &&
                                  emailVerified.GetBoolean();

                        if (context.Identity is not null)
                        {
                            if (verified)
                                context.Identity.AddClaim(new Claim("google_email_verified", "true"));

                            if (context.User.TryGetProperty("picture", out var pictureElement) &&
                                pictureElement.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                var pictureUrl = pictureElement.GetString();
                                if (!string.IsNullOrWhiteSpace(pictureUrl))
                                    context.Identity.AddClaim(new Claim("google_picture", pictureUrl));
                            }
                        }

                        return Task.CompletedTask;
                    };
                    options.Events.OnRemoteFailure = context =>
                    {
                        context.HandleResponse();
                        var operation = context.Properties is not null &&
                                        context.Properties.Items.TryGetValue(
                                            "wellplayd_google_operation",
                                            out var operationValue)
                            ? operationValue
                            : null;
                        if (string.Equals(operation, "link", StringComparison.Ordinal))
                        {
                            var frontendOrigin = new Uri(googleSettings.FrontendCallbackUrl)
                                .GetLeftPart(UriPartial.Authority);
                            context.Response.Redirect(QueryHelpers.AddQueryString(
                                frontendOrigin + "/settings",
                                new Dictionary<string, string?>
                                {
                                    ["section"] = "integrations",
                                    ["googleLink"] = "error",
                                    ["googleError"] = "google_cancelled"
                                }));
                        }
                        else
                        {
                            context.Response.Redirect(QueryHelpers.AddQueryString(
                                googleSettings.FrontendCallbackUrl,
                                "error",
                                "google_cancelled"));
                        }

                        return Task.CompletedTask;
                    };
                });
            }

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

            builder.Services.AddCachingInfrastructure(builder.Configuration);
            builder.Services.AddPersistenceServices(builder.Configuration);

            var healthChecks = builder.Services
                .AddHealthChecks();

            // Azure Health Check may call /health/ready periodically. The database
            // checks themselves wake Azure SQL Serverless, so they are excluded
            // while the temporary SQL idle mode is enabled.
            if (!sqlIdleModeSettings.Enabled)
            {
                healthChecks
                    .AddDbContextCheck<AppDbContext>(
                        "database",
                        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                        tags: ["ready"])
                    .AddCheck<DatabaseMigrationHealthCheck>(
                        "database-migrations",
                        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                        tags: ["ready"]);
            }

            healthChecks
                .AddCheck<StorageHealthCheck>(
                    "storage",
                    failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                    tags: ["ready"])
                .AddCheck<ClamAvHealthCheck>(
                    "clamav",
                    failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                    tags: ["ready"]);

            if (cacheSettings.RedisEnabled)
            {
                healthChecks.AddCheck<RedisHealthCheck>(
                    "redis",
                    failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                    tags: ["ready"]);
            }

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

            if (args.Any(argument =>
                    string.Equals(argument, "--migrate-only", StringComparison.OrdinalIgnoreCase)))
            {
                await DatabaseMigrationRunner.ApplyPendingMigrationsAsync(app.Services);
                return;
            }

            // Roles are already persisted in the database. Re-seeding them on every
            // idle-mode restart would wake Azure SQL even when there is no user traffic.
            if (!sqlIdleModeSettings.Enabled)
                await EnsureRolesAsync(app.Services);

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            if (forwardedHeadersSettings.Enabled)
                app.UseForwardedHeaders();

            app.UseRequestLocalization();
            app.UseMiddleware<RequestTelemetryMiddleware>();
            app.UseMiddleware<GlobalExceptionMiddleware>();
            app.UseMiddleware<SecurityHeadersMiddleware>();
            app.UseResponseCompression();

            if (!app.Environment.IsDevelopment())
                app.UseHsts();

            // Liveness/readiness are queried directly inside the container over HTTP.
            // All user-facing routes still enforce HTTPS after trusted forwarded headers
            // have been processed.
            app.UseWhen(
                context => !context.Request.Path.StartsWithSegments("/health"),
                branch => branch.UseHttpsRedirection());

            var fileStorageSettings = app.Services
                .GetRequiredService<FileStorageSettings>();
            var publicUploadsPath = Path.Combine(
                fileStorageSettings.WebRootPath,
                "uploads");
            Directory.CreateDirectory(publicUploadsPath);
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(publicUploadsPath),
                RequestPath = "/uploads",
                OnPrepareResponse = context =>
                {
                    context.Context.Response.Headers["Cache-Control"] =
                        "public,max-age=604800,immutable";
                    context.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                }
            });

            // Keep the dedicated /uploads middleware first so its cache and
            // security headers are not bypassed by the default web-root provider.
            app.UseStaticFiles();

            app.UseCors("Frontend");
            app.UseAuthentication();
            app.UseRateLimiter();
            app.UseAuthorization();

            app.MapHealthChecks(
                    "/health/live",
                    new HealthCheckOptions
                    {
                        Predicate = _ => false,
                        ResponseWriter = HealthCheckResponseWriter.WritePublicAsync
                    })
                .AllowAnonymous();

            app.MapHealthChecks(
                    "/health/ready",
                    new HealthCheckOptions
                    {
                        Predicate = registration => registration.Tags.Contains("ready"),
                        ResponseWriter = HealthCheckResponseWriter.WritePublicAsync
                    })
                .AllowAnonymous();

            app.MapHealthChecks(
                    "/health/details",
                    new HealthCheckOptions
                    {
                        Predicate = registration => registration.Tags.Contains("ready"),
                        ResponseWriter = HealthCheckResponseWriter.WriteDetailedAsync
                    })
                .RequireAuthorization(CapabilityNames.AccessAdminPanel);

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
}
