namespace Ludora.Application.Interfaces;

public interface IGroqClient
{
    bool IsEnabled { get; }

    Task<string> GetJsonCompletionAsync(
        string systemPrompt,
        string userPayloadJson,
        CancellationToken cancellationToken = default);
}
