namespace Ludora.Persistence.Extensions
{
    using Ludora.Application.Exceptions;

    public static class StreamExtension
    {
        public static async Task<byte[]> ToByteArrayAsync(
            this Stream stream,
            long declaredLength,
            long maxLength,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);

            if (declaredLength is <= 0 or > int.MaxValue ||
                declaredLength > maxLength)
            {
                throw new BadRequestException("Fayl ölçüsü düzgün deyil.");
            }

            var bytes = GC.AllocateUninitializedArray<byte>((int)declaredLength);
            var offset = 0;

            while (offset < bytes.Length)
            {
                var read = await stream.ReadAsync(
                    bytes.AsMemory(offset),
                    cancellationToken);

                if (read == 0)
                    break;

                offset += read;
            }

            if (offset < bytes.Length)
                Array.Resize(ref bytes, offset);

            var extraByte = new byte[1];

            if (await stream.ReadAsync(extraByte, cancellationToken) > 0)
            {
                throw new BadRequestException(
                    "Faylın faktiki ölçüsü elan edilən ölçüyə uyğun deyil.");
            }

            return bytes;
        }
    }
}
