namespace Ludora.Application.Extensions
{
    using Ludora.Application.DTOs;
    using Ludora.Application.Exceptions;

    public static class FileValidationExtensions
    {
        private static readonly byte[] PngSignature =
        {
            0x89,
            0x50,
            0x4E,
            0x47,
            0x0D,
            0x0A,
            0x1A,
            0x0A
        };

        public static void ValidateUpload(
            this ProfileImageUploadDTO file,
            long maxSize,
            FileSize fileSize)
        {
            ValidateUploadCore(
                file.Stream,
                file.Length,
                maxSize,
                fileSize,
                "Profil şəkli");
        }

        public static void ValidateUpload(
            this PostImageUploadDTO file,
            long maxSize,
            FileSize fileSize)
        {
            ValidateUploadCore(
                file.Stream,
                file.Length,
                maxSize,
                fileSize,
                "Post şəkli");
        }


        public static void ValidateUpload(
            this UserReportEvidenceUploadDTO file,
            long maxSize,
            FileSize fileSize)
        {
            ValidateUploadCore(
                file.Stream,
                file.Length,
                maxSize,
                fileSize,
                "Şikayət sübutu");
        }

        public static void ValidateActualSize(
            this byte[] bytes,
            long maxSize,
            FileSize fileSize)
        {
            if (bytes.Length == 0) throw new BadRequestException("Yüklənən fayl boşdur.");

            if (bytes.LongLength > ConvertToBytes(maxSize, fileSize))
            {
                throw new BadRequestException($"Şəkil maksimum {maxSize} {fileSize} ola bilər.");
            }
        }

        public static string GetImageExtension(this byte[] bytes)
        {
            if (bytes.IsJpeg())
                return ".jpg";

            if (bytes.IsPng())
                return ".png";

            if (bytes.IsWebP())
                return ".webp";

            throw new BadRequestException(
                "Yalnız JPEG, PNG və WEBP şəkilləri qəbul edilir.");
        }

        private static bool IsJpeg(this byte[] bytes)
        {
            return bytes.Length >= 3
                && bytes[0] == 0xFF
                && bytes[1] == 0xD8
                && bytes[2] == 0xFF;
        }

        private static bool IsPng(this byte[] bytes)
        {
            return bytes.Length >= PngSignature.Length
                && bytes.AsSpan(0, PngSignature.Length)
                    .SequenceEqual(PngSignature);
        }

        private static bool IsWebP(this byte[] bytes)
        {
            return bytes.Length >= 12
                && bytes[0] == 'R'
                && bytes[1] == 'I'
                && bytes[2] == 'F'
                && bytes[3] == 'F'
                && bytes[8] == 'W'
                && bytes[9] == 'E'
                && bytes[10] == 'B'
                && bytes[11] == 'P';
        }

        private static void ValidateUploadCore(
            Stream stream,
            long length,
            long maxSize,
            FileSize fileSize,
            string fieldName)
        {
            if (stream == Stream.Null || length <= 0)
                throw new BadRequestException($"{fieldName} seçilməyib.");

            if (length > ConvertToBytes(maxSize, fileSize))
                throw new BadRequestException($"{fieldName} maksimum {maxSize} {fileSize} ola bilər.");
        }

        private static long ConvertToBytes(long size, FileSize fileSize)
        {
            return fileSize switch
            {
                FileSize.KB => checked(size * 1024L),
                FileSize.MB => checked(size * 1024L * 1024L),
                FileSize.GB => checked(size * 1024L * 1024L * 1024L),
                _ => throw new ArgumentOutOfRangeException(nameof(fileSize))
            };
        }
    }
}
