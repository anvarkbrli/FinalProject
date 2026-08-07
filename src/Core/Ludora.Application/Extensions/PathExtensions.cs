namespace Ludora.Application.Extensions
{
    public static class PathExtensions
    {
        public static string? GetSafeFullPath(this string rootPath, string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;

            var fullRootPath = Path.GetFullPath(rootPath);

            var safeRelativePath = relativePath
                .TrimStart('/', '\\')
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            var fullPath = Path.GetFullPath(Path.Combine(fullRootPath, safeRelativePath));

            var allowedRoot = fullRootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            return fullPath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase) ? fullPath : null;
        }
    }
}
