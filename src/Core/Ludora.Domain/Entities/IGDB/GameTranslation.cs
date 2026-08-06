using Ludora.Domain.Entities.Base;

namespace Ludora.Domain.Entities.IGDB
{
    public sealed class GameTranslation : BaseEntity
    {
        public long IgdbGameId { get; set; }
        public string LanguageCode { get; set; } = null!;
        public string FieldName { get; set; } = null!;
        public string TranslatedText { get; set; } = null!;
        public string SourceTextHash { get; set; } = null!;
    }
}
