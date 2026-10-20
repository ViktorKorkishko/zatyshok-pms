using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Zatyshok.Infrastructure.Persistence;

public static class TableBuilderExtensions
{
    /// <summary>
    /// Enums are stored as strings (<c>HasConversion&lt;string&gt;()</c>), so the database alone would
    /// accept any text. This adds <c>CK_&lt;Table&gt;_&lt;Column&gt;</c>: <c>"Column" IN ('Value1', 'Value2', …)</c>
    /// built from the enum member names (12.4). A new enum value therefore needs a migration that
    /// recreates the constraint. For a nullable column NULL passes, because <c>NULL IN (…)</c> is not false.
    /// </summary>
    public static TableBuilder<TEntity> HasEnumCheck<TEntity, TEnum>(this TableBuilder<TEntity> table, string column)
        where TEntity : class
        where TEnum : struct, Enum
    {
        var values = string.Join(", ", Enum.GetNames<TEnum>().Select(n => $"'{n}'"));
        table.HasCheckConstraint($"CK_{table.Name}_{column}", $"\"{column}\" IN ({values})");
        return table;
    }
}
