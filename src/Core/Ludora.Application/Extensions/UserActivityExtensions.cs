using Ludora.Domain.Entities.User;
using Ludora.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Application.Extensions
{
    public static class UserActivityExtensions
    {
        public static UserActivity AddUserActivity(
            this DbSet<UserActivity> userActivities,
            Guid userId,
            UserActivityType type,
            Guid? targetUserId = null,
            CatalogSource? gameSource = null,
            long? gameId = null,
            long? reviewId = null,
            UserGameStatus? status = null,
            double? rating = null,
            long? postId = null,
            int? gameListId = null,
            string? subjectTitle = null,
            DateTime? createdAt = null)
        {
            ArgumentNullException.ThrowIfNull(userActivities);

            var activity = new UserActivity
            {
                UserId = userId,
                Type = type,
                TargetUserId = targetUserId,
                GameSource = gameSource,
                GameId = gameId,
                ReviewId = reviewId,
                Status = status,
                Rating = rating,
                PostId = postId,
                GameListId = gameListId,
                SubjectTitle = string.IsNullOrWhiteSpace(subjectTitle)
                    ? null
                    : subjectTitle.Trim(),
                CreatedAt = createdAt ?? DateTime.UtcNow
            };

            userActivities.Add(activity);
            return activity;
        }
    }
}
