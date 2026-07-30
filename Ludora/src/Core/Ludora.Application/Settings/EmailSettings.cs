namespace Ludora.Application.Settings;

public sealed class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "WellPlayd";
    public bool EnableSsl { get; set; } = true;

    // API endpoint-ləri ilə email dəyişiklik təsdiqi kimi server linkləri üçün qalır.
    public string PublicApiBaseUrl { get; set; } = string.Empty;

    // Qeydiyyat emaili istifadəçini JSON qaytaran API endpoint-inə yox,
    // istifadəçi-dostu frontend təsdiq səhifəsinə aparır.
    public string PublicFrontendBaseUrl { get; set; } = string.Empty;

    // SMTP server müvəqqəti xəta verdikdə eyni request daxilində ediləcək cəhd sayı.
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 2;

    // SmtpClient.Timeout yalnız sinxron Send üçün keçərlidir. Async göndərişdə
    // ayrıca CancellationToken timeout-u istifadə olunur.
    public int SendTimeoutSeconds { get; set; } = 30;
}
