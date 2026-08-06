using Ludora.API.Configuration;

namespace Ludora.API.Tests
{
    public sealed class DeploymentPathResolverTests
    {
        [Fact]
        public void Resolve_RelativePath_UsesContentRoot()
        {
            var contentRoot = Path.Combine(Path.GetTempPath(), "wellplayd-tests");

            var result = DeploymentPathResolver.Resolve(contentRoot, "private-storage");

            Assert.Equal(
                Path.GetFullPath(Path.Combine(contentRoot, "private-storage")),
                result);
        }

        [Fact]
        public void Resolve_EmptyPath_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                DeploymentPathResolver.Resolve(Path.GetTempPath(), " "));
        }
    }
}
