using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Ludora.API.Middlewares;
using Ludora.Application.Settings;
using Ludora.Infrastructure.IGDB;
using Ludora.Persistence.ServiceRegistration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
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
        });

        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("Auth", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        });

        builder.Services.AddPersistenceServices(builder.Configuration);
        builder.Services.AddIgdbIntegration(builder.Configuration);

        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
        {
            if (origins.Length == 0)
                throw new InvalidOperationException("Cors:AllowedOrigins boş ola bilməz.");
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        }));

        var app = builder.Build();
        await EnsureRolesAsync(app.Services);

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseMiddleware<GlobalExceptionMiddleware>();
        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseCors("Frontend");
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.RunAsync();
    }

    private static async Task EnsureRolesAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();

        var roleManager =
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        string[] roleNames =
        [
            "Admin",
            "User"
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
