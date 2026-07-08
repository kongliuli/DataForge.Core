using System.Collections.Generic;
using System.Threading;

namespace DataForge.Core.Core.Infrastructure;

public static class SqlIdentifier
{
    public static string ValidateTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new DataSourceException("Table name must not be empty.", "SQL");
        }

        foreach (var ch in tableName)
        {
            if (!char.IsLetterOrDigit(ch) && ch != '_')
            {
                throw new DataSourceException(
                    $"Invalid table name '{tableName}'. Only letters, digits, and underscore are allowed.",
                    "SQL",
                    "INVALID_IDENTIFIER");
            }
        }

        return tableName;
    }
}
