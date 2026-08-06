namespace Ludora.Application.Interfaces
{
    public interface ITranslationProvider
    {
        bool IsConfigured { get; }

        Task<string?> TranslateAsync(
            string text,
            string sourceLanguage,
            string targetLanguage,
            CancellationToken cancellationToken = default);
    }
}
