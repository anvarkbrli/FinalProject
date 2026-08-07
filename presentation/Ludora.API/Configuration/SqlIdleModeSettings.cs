namespace Ludora.API.Configuration
{
    /// <summary>
    /// Temporary cost-saving mode for Azure SQL Serverless.
    /// When enabled, recurring SQL-backed hosted workers and the lifetime
    /// SQL application lock are not started. User-driven API requests can
    /// still access SQL normally and wake the database when needed.
    /// </summary>
    public sealed class SqlIdleModeSettings
    {
        public const string SectionName = "SqlIdleMode";

        public bool Enabled { get; set; }
    }
}
