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
    public string FromName { get; set; } = "Ludora";
    public bool EnableSsl { get; set; } = true;
    public string PublicApiBaseUrl { get; set; } = string.Empty;
}
