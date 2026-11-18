using System.Reflection;
using Laundry.BuildingBlocks.Infrastructure.Persistence;
using Xunit;

namespace Laundry.Tests.Architecture;

public class AssemblyRuleTests
{
    private static IReadOnlyList<string> Modules => BackendLayout.Modules;

    private static Assembly Load(string module, string layer) => Assembly.Load($"Laundry.{module}.{layer}");

    [Fact]
    public void ModuleAssembliesRespectReferenceRules()
    {
        var violations = new List<string>();

        foreach (var module in Modules)
        {
            foreach (var layer in BackendLayout.Layers)
            {
                var assembly = Load(module, layer);
                var referenced = assembly.GetReferencedAssemblies().Select(a => a.Name).OfType<string>();
                violations.AddRange(AssemblyRules.References(assembly.GetName().Name!, referenced, Modules));
            }
        }

        Assertions.NoViolations(violations);
    }

    [Fact]
    public void EachModuleOwnsExactlyOneDbContextWithItsOwnSchema()
    {
        var violations = new List<string>();
        var schemas = new List<(string Module, string? Schema)>();

        foreach (var module in Modules)
        {
            var contexts = BackendLayout.Layers
                .SelectMany(layer => Load(module, layer).GetTypes())
                .Where(t => !t.IsAbstract && typeof(ModuleDbContext).IsAssignableFrom(t))
                .Select(t => new DbContextInfo(t.Name, t.Namespace, ReadSchemaName(t)))
                .ToList();

            violations.AddRange(AssemblyRules.DbContexts(module, contexts));
            schemas.AddRange(contexts.Select(c => (module, c.SchemaName)));
        }

        violations.AddRange(AssemblyRules.DistinctSchemas(schemas));

        Assertions.NoViolations(violations);
    }

    private static string? ReadSchemaName(Type type) =>
        type.GetField("SchemaName", BindingFlags.Public | BindingFlags.Static)?.GetRawConstantValue() as string;
}
