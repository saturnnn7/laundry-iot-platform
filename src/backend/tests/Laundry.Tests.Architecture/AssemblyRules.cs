namespace Laundry.Tests.Architecture;

public sealed record DbContextInfo(string TypeName, string? Namespace, string? SchemaName);

public static class AssemblyRules
{
    public static IEnumerable<string> References(
        string assemblyName,
        IEnumerable<string> referencedNames,
        IReadOnlyCollection<string> modules)
    {
        var parts = assemblyName.Split('.');
        if (parts.Length != 3 || parts[0] != "Laundry")
        {
            yield break;
        }

        var module = parts[1];
        var layer = parts[2];

        foreach (var referenced in referencedNames)
        {
            var other = referenced.Split('.');

            if (other.Length >= 3
                && other[0] == "Laundry"
                && modules.Contains(other[1])
                && other[1] != module
                && other[2] != "Contracts")
            {
                yield return $"{assemblyName}: references '{referenced}', only Contracts of another module is allowed";
            }

            if (layer == "Domain" && !IsAllowedForDomain(referenced))
            {
                yield return $"{assemblyName}: references '{referenced}', a Domain assembly may only use System, netstandard, mscorlib and Laundry.BuildingBlocks.Domain";
            }

            if (layer == "Application" && referenced.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
            {
                yield return $"{assemblyName}: references '{referenced}', an Application assembly must not use ASP.NET Core";
            }
        }
    }

    public static IEnumerable<string> DbContexts(string module, IReadOnlyList<DbContextInfo> contexts)
    {
        if (contexts.Count != 1)
        {
            yield return $"Module '{module}': expected exactly one DbContext implementing IModuleDbContext, found {contexts.Count}";
            yield break;
        }

        var context = contexts[0];
        var expectedName = $"{module}DbContext";
        var expectedNamespace = $"Laundry.{module}.Infrastructure";
        var expectedSchema = module.ToLowerInvariant();

        if (context.TypeName != expectedName)
        {
            yield return $"Module '{module}': DbContext is named '{context.TypeName}', expected '{expectedName}'";
        }

        if (context.Namespace != expectedNamespace)
        {
            yield return $"Module '{module}': DbContext is in '{context.Namespace}', expected '{expectedNamespace}'";
        }

        if (context.SchemaName != expectedSchema)
        {
            yield return $"Module '{module}': SchemaName is '{context.SchemaName}', expected '{expectedSchema}'";
        }
    }

    public static IEnumerable<string> DistinctSchemas(IEnumerable<(string Module, string? Schema)> schemas) =>
        schemas
            .GroupBy(s => s.Schema)
            .Where(g => g.Count() > 1)
            .Select(g => $"Schema '{g.Key}' is used by several modules: {string.Join(", ", g.Select(s => s.Module))}");

    private static bool IsAllowedForDomain(string referenced) =>
        referenced.StartsWith("System", StringComparison.Ordinal)
        || referenced == "netstandard"
        || referenced == "mscorlib"
        || referenced == "Laundry.BuildingBlocks.Domain";
}
