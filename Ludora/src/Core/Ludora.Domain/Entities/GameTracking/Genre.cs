using Ludora.Domain.Entities.Base;

namespace Ludora.Domain.Entities.GameTracking
{
    public class Genre : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
    }
}
