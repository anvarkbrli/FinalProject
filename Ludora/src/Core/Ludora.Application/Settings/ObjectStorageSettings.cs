namespace Ludora.Application.Settings;

public sealed class ObjectStorageSettings
{
    public const string SectionName = "ObjectStorage";

    public string Provider { get; set; } = "Local";
    public string BucketName { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
    public string ServiceUrl { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string PublicBaseUrl { get; set; } = string.Empty;
    public bool ForcePathStyle { get; set; }
    public bool UseHttp { get; set; }
    public string PublicPrefix { get; set; } = "public";
    public string PrivatePrefix { get; set; } = "private";
}
