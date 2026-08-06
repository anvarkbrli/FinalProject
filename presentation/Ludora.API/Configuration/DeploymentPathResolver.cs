namespace Ludora.API.Configuration
{
    public static class DeploymentPathResolver
    {
        public static string Resolve(string contentRootPath, string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
                throw new InvalidOperationException("Storage path cannot be empty.");

            return Path.GetFullPath(
                Path.IsPathRooted(configuredPath)
                    ? configuredPath
                    : Path.Combine(contentRootPath, configuredPath));
        }
    }
}
