namespace Ludora.Application.Extensions;

using Ludora.Application.Exceptions;
using Ludora.Domain.Enums;

public static class ValidationExtensions
{
    public static void EnsureValidId(this int id, string parameterName = "id")
    {
        if (id < 1)
        {
            throw new BadRequestException(
                $"{parameterName} 1-dən kiçik ola bilməz.");
        }
    }

    public static void EnsureValidCatalogSource(this CatalogSource source)
    {
        if (!Enum.IsDefined(source))
            throw new BadRequestException("Məlumat mənbəyi düzgün deyil.");
    }

    public static void EnsureValidCatalogReference(
        this long id,
        CatalogSource source,
        string entityName)
    {
        source.EnsureValidCatalogSource();

        if (id < 1)
            throw new BadRequestException($"{entityName} ID-si düzgün deyil.");

        if (source == CatalogSource.Local && id > int.MaxValue)
        {
            throw new BadRequestException(
                $"Local {entityName.ToLowerInvariant()} ID-si düzgün deyil.");
        }
    }

    public static void EnsureDefined<TEnum>(
        this TEnum value,
        string fieldName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new BadRequestException($"{fieldName} düzgün deyil.");
    }

    public static string NormalizeName(
        this string? name,
        int maxLength,
        string entityName)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BadRequestException($"{entityName} adı boş ola bilməz.");

        var normalizedName = string.Join(
            " ",
            name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (normalizedName.Length > maxLength)
        {
            throw new BadRequestException(
                $"{entityName} adı maksimum {maxLength} simvol ola bilər.");
        }

        return normalizedName;
    }
}
