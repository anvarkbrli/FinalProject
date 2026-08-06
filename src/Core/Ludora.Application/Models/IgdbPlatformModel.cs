namespace Ludora.Application.Models
{
    public sealed record IgdbPlatformModel
    {
        public long Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }
}
