using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ludora.Application.Extensions
{
    public static class ValidationExtensions
    {
        public static void EnsureValidId(this int id, string parameterName = "id")
        {
            if (id < 1)
            {
                throw new ArgumentException(
                    $"{parameterName} 1-dən kiçik ola bilməz.",
                    parameterName);
            }
        }
        public static string NormalizeName(this string? name, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Platforma adı boş ola bilməz.");

            var normalizedName = string.Join(
                " ",
                name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

            if (normalizedName.Length > maxLength)
            {
                throw new ArgumentException(
                    $"Platforma adı maksimum {maxLength} simvol ola bilər.");
            }

            return normalizedName;
        }

    }
}
