namespace ListingWatcher;

/// <summary>
/// Minimal representation of UI settings used to load the database connection string
/// in the worker service without depending on the main application project.
/// </summary>
public class UiSettings
{
    public string DbServer { get; set; } = string.Empty;
    public string DbName { get; set; } = string.Empty;
    public string DbUser { get; set; } = string.Empty;
    public string DbPassword { get; set; } = string.Empty;

    /// <summary>
    /// Builds a SQL Server connection string from the configured values.
    /// Returns an empty string if required fields are missing.
    /// </summary>
    public string GetConnectionString()
    {
        if (string.IsNullOrWhiteSpace(DbServer) ||
            string.IsNullOrWhiteSpace(DbName) ||
            string.IsNullOrWhiteSpace(DbUser))
        {
            return string.Empty;
        }

        return $"Server={DbServer};Database={DbName};User Id={DbUser};Password={DbPassword};TrustServerCertificate=True";
    }
}

