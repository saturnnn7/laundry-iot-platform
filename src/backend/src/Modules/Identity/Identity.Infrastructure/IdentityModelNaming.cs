using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Laundry.Identity.Infrastructure;

// Renames the Identity entity types to the snake_case names used in the rest of the schema.
internal static class IdentityModelNaming
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var table = TableNameFor(entityType.ClrType);
            if (table is null)
            {
                continue;
            }

            entityType.SetTableName(table);

            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }

            foreach (var key in entityType.GetKeys())
            {
                key.SetName("pk_" + table);
            }

            foreach (var index in entityType.GetIndexes())
            {
                index.SetDatabaseName("ix_" + table + "_" + string.Join('_', index.Properties.Select(p => ToSnakeCase(p.Name))));
            }

            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                var principal = TableNameFor(foreignKey.PrincipalEntityType.ClrType) ?? foreignKey.PrincipalEntityType.GetTableName();
                var columns = string.Join('_', foreignKey.Properties.Select(p => ToSnakeCase(p.Name)));
                foreignKey.SetConstraintName($"fk_{table}_{principal}_{columns}");
            }
        }
    }

    internal static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 4);

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                var startsWord = i > 0
                    && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])
                        || (char.IsUpper(name[i - 1]) && i + 1 < name.Length && char.IsLower(name[i + 1])));
                if (startsWord)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static string? TableNameFor(Type clrType) =>
        clrType == typeof(AppUser) ? "users"
        : clrType == typeof(IdentityUserClaim<Guid>) ? "user_claims"
        : clrType == typeof(IdentityUserLogin<Guid>) ? "user_logins"
        : clrType == typeof(IdentityUserToken<Guid>) ? "user_tokens"
        : null;
}
