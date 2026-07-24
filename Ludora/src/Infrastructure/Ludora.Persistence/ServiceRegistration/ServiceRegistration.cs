using Ludora.Application.Interfaces;
using Ludora.Domain.Entities.User;
using Ludora.Persistence.Data;
using Ludora.Persistence.Services;
using Ludora.Persistence.Services.Admin;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ludora.Persistence.ServiceRegistration;

public static class ServiceRegistration
{
    public static void AddPersistenceServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContextPool<AppDbContext>(
            options => options.UseSqlServer(
                configuration.GetConnectionString("default"),
                sqlOptions =>
                {
                    sqlOptions.MaxBatchSize(100);
                }),
            poolSize: 64);

        services.AddIdentityCore<AppUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = true;
            options.Password.RequiredLength = 6;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })

        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromHours(24));

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IAdminPostService, AdminPostService>();
        services.AddScoped<IAdminUserReportService, AdminUserReportService>();
        services.AddScoped<IAdminModerationAuditService, AdminModerationAuditService>();
        services.AddScoped<IUserWarningService, UserWarningService>();
        services.AddScoped<IUserReportService, UserReportService>();
        services.AddScoped<IAnnouncementService, AnnouncementService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IFollowService, FollowService>();
        services.AddScoped<IGameListService, GameListService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IUserGameService, UserGameService>();
        services.AddScoped<ISteamService, SteamService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IBadgeService, BadgeService>();
        services.AddScoped<IUserActivityService, UserActivityService>();
        services.AddScoped<IPostService, PostService>();
        services.AddScoped<IPostInteractionService, PostInteractionService>();
        services.AddScoped<IGameListInteractionService, GameListInteractionService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IReviewInteractionService, ReviewInteractionService>();
        services.AddScoped<IGlobalSearchService, GlobalSearchService>();
        services.AddScoped<IForumCategoryService, ForumCategoryService>();
        services.AddScoped<IDiscussionService, DiscussionService>();
        services.AddScoped<IDiscussionReplyService, DiscussionReplyService>();
        services.AddScoped<IForumCommunityService, ForumCommunityService>();

        services.AddScoped<IGameService, GameService>();
        services.AddScoped<IGameCommunityService, GameCommunityService>();
        services.AddScoped<IGenreService, GenreService>();
        services.AddScoped<IPlatformService, PlatformService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IBlockService, BlockService>();
    }
}
