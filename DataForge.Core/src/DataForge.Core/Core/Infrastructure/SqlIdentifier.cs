using System.Collections.Generic;
using System.Threading;

namespace DataForge.Core.Core.Infrastructure;

public static class SqlIdentifier
{
    /// <summary>
    /// Compatibility wrapper over <see cref="SqlIdentifiers.Validate"/> for callers that still use this name.
    /// </summary>
    public static string ValidateTableName(string tableName)
    {
        try
        {
            return SqlIdentifiers.Validate(tableName, nameof(tableName));
        }
        catch (ArgumentException ex)
        {
            throw new DataSourceException(ex.Message, "SQL", "INVALID_IDENTIFIER");
        }
    }
}
