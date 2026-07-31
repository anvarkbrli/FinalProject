using System.ComponentModel.DataAnnotations;

namespace Ludora.Application.DTOs.JWT;

public sealed record EmailDiagnosticsDTO
{
    public bool Enabled { get; init; }
    public string Environment { get; init; } = string.Empty;
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }
    public bool EnableSsl { get; init; }
    public bool UserNameConfigured { get; init; }
    public bool PasswordConfigured { get; init; }
    public string FromEmail { get; init; } = string.Empty;
    public string PublicApiBaseUrl { get; init; } = string.Empty;
    public string PublicFrontendBaseUrl { get; init; } = string.Empty;
    public int MaxRetryAttempts { get; init; }
    public int SendTimeoutSeconds { get; init; }
}

public sealed record EmailDeliveryTestDTO
{
    [Required(ErrorMessage = "Test email ünvanını daxil edin.")]
    [EmailAddress(ErrorMessage = "Email ünvanının formatı düzgün deyil.")]
    [StringLength(128, ErrorMessage = "Email ünvanı maksimum 128 simvol ola bilər.")]
    public string Email { get; init; } = string.Empty;
}
