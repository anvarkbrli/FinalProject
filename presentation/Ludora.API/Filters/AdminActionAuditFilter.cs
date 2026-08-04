using System.Security.Claims;
using Ludora.Application.Common;
using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Ludora.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Ludora.API.Filters;

public sealed class AdminActionAuditFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> AlreadyAuditedActions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "AdminUsers.Warn",
            "AdminUsers.Ban",
            "AdminUsers.Unban",
            "AdminUsers.ChangeRole",
            "AdminUserReports.UpdateStatus"
        };

    private static readonly string[] ResourceIdKeys =
    [
        "badgeId",
        "postId",
        "announcementId",
        "discussionId",
        "replyId",
        "reportId",
        "id",
        "relatedId"
    ];

    private readonly AppDbContext _context;
    private readonly ILogger<AdminActionAuditFilter> _logger;

    public AdminActionAuditFilter(
        AppDbContext context,
        ILogger<AdminActionAuditFilter> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (!ShouldAudit(context))
        {
            await next();
            return;
        }

        var executedContext = await next();

        if (!WasSuccessful(executedContext))
            return;

        var adminUserIdValue = context.HttpContext.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(adminUserIdValue, out var adminUserId))
            return;

        var descriptor = (ControllerActionDescriptor)context.ActionDescriptor;
        var targetUserId = TryGetTargetUserId(context);
        var resourceType = GetResourceType(descriptor, context);
        var resourceId = GetResourceId(context, executedContext);
        var resourceName = GetResourceName(context, executedContext);
        var operation = GetOperation(descriptor.ActionName, resourceType);

        _context.ModeratorActionLogs.Add(new ModeratorActionLog
        {
            AdminUserId = adminUserId,
            TargetUserId = targetUserId,
            ActionType = ModeratorActionType.AdministrativeAction,
            Operation = operation,
            ResourceType = resourceType,
            ResourceId = resourceId,
            ResourceName = resourceName,
            CreatedAtUtc = DateTime.UtcNow
        });

        try
        {
            await _context.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Admin audit qeydi saxlanmadı. AdminUserId: {AdminUserId}, " +
                "Controller: {Controller}, Action: {Action}",
                adminUserId,
                descriptor.ControllerName,
                descriptor.ActionName);
            throw;
        }
    }

    private static bool ShouldAudit(ActionExecutingContext context)
    {
        if (HttpMethods.IsGet(context.HttpContext.Request.Method) ||
            HttpMethods.IsHead(context.HttpContext.Request.Method) ||
            HttpMethods.IsOptions(context.HttpContext.Request.Method))
        {
            return false;
        }

        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
            return false;

        var actionKey = $"{descriptor.ControllerName}.{descriptor.ActionName}";
        if (AlreadyAuditedActions.Contains(actionKey))
            return false;

        return context.ActionDescriptor.EndpointMetadata
            .OfType<IAuthorizeData>()
            .Where(x => !string.IsNullOrWhiteSpace(x.Roles))
            .SelectMany(x => x.Roles!.Split(
                ',',
                StringSplitOptions.TrimEntries |
                StringSplitOptions.RemoveEmptyEntries))
            .Any(role =>
                role.Equals(RoleNames.Admin, StringComparison.OrdinalIgnoreCase) ||
                role.Equals(RoleNames.Moderator, StringComparison.OrdinalIgnoreCase));
    }

    private static bool WasSuccessful(ActionExecutedContext context)
    {
        if (context.Canceled ||
            (context.Exception is not null && !context.ExceptionHandled))
        {
            return false;
        }

        var statusCode = context.Result is IStatusCodeActionResult statusResult
            ? statusResult.StatusCode ?? StatusCodes.Status200OK
            : StatusCodes.Status200OK;

        return statusCode is >= 200 and < 300;
    }

    private static Guid? TryGetTargetUserId(ActionExecutingContext context)
    {
        foreach (var key in new[] { "userId", "targetUserId" })
        {
            if (context.RouteData.Values.TryGetValue(key, out var value) &&
                Guid.TryParse(Convert.ToString(value), out var userId))
            {
                return userId;
            }

            if (context.ActionArguments.TryGetValue(key, out value) &&
                Guid.TryParse(Convert.ToString(value), out userId))
            {
                return userId;
            }
        }

        return null;
    }

    private static string GetResourceType(
        ControllerActionDescriptor descriptor,
        ActionExecutingContext context)
    {
        return descriptor.ControllerName switch
        {
            "Games" => "Oyun",
            "Genres" => "Janr",
            "Platforms" => "Platforma",
            "Companies" => "Şirkət",
            "AdminBadges" => "Nişan",
            "AdminAnnouncements" => "Elan",
            "AdminPosts" => "Post",
            "ForumCategories" => "Forum kateqoriyası",
            "AdminForum" when context.HttpContext.Request.Path.Value?
                .Contains("/replies/", StringComparison.OrdinalIgnoreCase) == true
                => "Forum cavabı",
            "AdminForum" => "Forum müzakirəsi",
            _ => descriptor.ControllerName
                .Replace("Admin", string.Empty, StringComparison.Ordinal)
        };
    }

    private static string GetOperation(
        string actionName,
        string resourceType)
    {
        return actionName switch
        {
            "Create" => $"{resourceType} yaratdı",
            "Update" => $"{resourceType} məlumatlarını redaktə etdi",
            "Delete" or "DeleteDiscussion" or "DeleteReply"
                => $"{resourceType} sildi",
            "Restore" or "RestoreDiscussion" or "RestoreReply"
                => $"{resourceType} bərpa etdi",
            "UpdateIcon" => "Nişan şəklini dəyişdi",
            "EvaluateUser" => "İstifadəçinin nişanlarını yenidən hesabladı",
            "AssignToUser" => "İstifadəçiyə nişan verdi",
            "RevokeFromUser" => "İstifadəçidən nişanı geri aldı",
            "Pin" => "Forum müzakirəsini bərkitdi",
            "Unpin" => "Forum müzakirəsinin bərkitməsini ləğv etdi",
            "Lock" => "Forum müzakirəsini kilidlədi",
            "Unlock" => "Forum müzakirəsinin kilidini açdı",
            "AddRelation" => "Oyuna əlaqəli kontent əlavə etdi",
            "RemoveRelation" => "Oyundan əlaqəli kontenti sildi",
            "UploadCoverImage" => "Oyun örtük şəkli yüklədi",
            _ => $"{resourceType} üzrə “{actionName}” əməliyyatını yerinə yetirdi"
        };
    }

    private static string? GetResourceId(
        ActionExecutingContext context,
        ActionExecutedContext executedContext)
    {
        foreach (var key in ResourceIdKeys)
        {
            if (context.RouteData.Values.TryGetValue(key, out var value) &&
                !string.IsNullOrWhiteSpace(Convert.ToString(value)))
            {
                return Convert.ToString(value);
            }
        }

        var resultValue = (executedContext.Result as ObjectResult)?.Value;
        return ReadProperty(resultValue, "Id");
    }

    private static string? GetResourceName(
        ActionExecutingContext context,
        ActionExecutedContext executedContext)
    {
        foreach (var value in context.ActionArguments.Values)
        {
            if (value is Microsoft.AspNetCore.Http.IFormFile)
                continue;

            var name = ReadDisplayName(value);
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        var resultValue = (executedContext.Result as ObjectResult)?.Value;
        return ReadDisplayName(resultValue);
    }

    private static string? ReadDisplayName(object? value)
    {
        return ReadProperty(value, "Name") ??
               ReadProperty(value, "Title") ??
               ReadProperty(value, "Code") ??
               ReadProperty(value, "FileName");
    }

    private static string? ReadProperty(object? value, string propertyName)
    {
        if (value is null)
            return null;

        var property = value.GetType().GetProperty(
            propertyName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.IgnoreCase);

        var propertyValue = property?.GetValue(value);
        var text = Convert.ToString(propertyValue)?.Trim();

        return string.IsNullOrWhiteSpace(text)
            ? null
            : text.Length <= 300
                ? text
                : text[..300];
    }
}
