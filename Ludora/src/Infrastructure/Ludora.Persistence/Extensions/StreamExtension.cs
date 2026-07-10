namespace Ludora.Persistence.Extensions;

public static class StreamExtension
{
    public static async Task<byte[]> ToByteArrayAsync(this Stream stream)
    {
        await using var memoryStream = new MemoryStream();

        await stream.CopyToAsync(memoryStream);

        return memoryStream.ToArray();
    }
}