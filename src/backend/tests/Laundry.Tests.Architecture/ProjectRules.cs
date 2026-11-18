namespace Laundry.Tests.Architecture;

public static class ProjectRules
{
    public const string DependencyInjectionAbstractions = "Microsoft.Extensions.DependencyInjection.Abstractions";

    private static readonly string[] ApplicationPackageAllowList = [DependencyInjectionAbstractions];

    public static IEnumerable<string> ModuleLayout(string module, IReadOnlyCollection<string> existingProjects) =>
        BackendLayout.Layers
            .Select(layer => $"{module}.{layer}")
            .Where(name => !existingProjects.Contains(name))
            .Select(name => $"Module '{module}': project '{name}' is missing");

    public static IEnumerable<string> RequiredModulesExist(IReadOnlyCollection<string> modules) =>
        BackendLayout.RequiredModules
            .Where(required => !modules.Contains(required))
            .Select(required => $"Module '{required}' is missing");

    public static IEnumerable<string> ModuleDomain(ProjectModel project, string module)
    {
        var violations = new List<string>();

        foreach (var reference in project.ProjectReferences.Where(r => r != "BuildingBlocks.Domain"))
        {
            violations.Add($"{project.Name}: project reference '{reference}' is not allowed, only BuildingBlocks.Domain");
        }

        if (!project.ProjectReferences.Contains("BuildingBlocks.Domain"))
        {
            violations.Add($"{project.Name}: must reference BuildingBlocks.Domain");
        }

        return violations.Concat(NoPackages(project)).Concat(NoFrameworks(project));
    }

    public static IEnumerable<string> ModuleContracts(ProjectModel project, string module) =>
        References(project, ["BuildingBlocks.Application", "BuildingBlocks.Domain"], [], allowOtherContracts: false)
            .Concat(NoPackages(project))
            .Concat(NoFrameworks(project));

    public static IEnumerable<string> ModuleApplication(ProjectModel project, string module, IReadOnlyCollection<string> modules) =>
        References(
                project,
                [$"{module}.Domain", $"{module}.Contracts", "BuildingBlocks.Domain", "BuildingBlocks.Application"],
                modules,
                allowOtherContracts: true)
            .Concat(project.Packages
                .Where(p => !ApplicationPackageAllowList.Contains(p))
                .Select(p => $"{project.Name}: package '{p}' is not allowed"))
            .Concat(NoFrameworks(project));

    public static IEnumerable<string> ModuleInfrastructure(ProjectModel project, string module, IReadOnlyCollection<string> modules) =>
        References(
                project,
                [
                    $"{module}.Application",
                    $"{module}.Domain",
                    $"{module}.Contracts",
                    "BuildingBlocks.Domain",
                    "BuildingBlocks.Application",
                    "BuildingBlocks.Infrastructure",
                ],
                modules,
                allowOtherContracts: true)
            .Concat(NoPackages(project))
            .Concat(NoFrameworks(project));

    public static IEnumerable<string> ModuleApi(ProjectModel project, string module, IReadOnlyCollection<string> modules) =>
        References(
                project,
                [
                    $"{module}.Application",
                    $"{module}.Infrastructure",
                    $"{module}.Contracts",
                    "BuildingBlocks.Application",
                    "BuildingBlocks.Api",
                ],
                modules,
                allowOtherContracts: true)
            .Concat(NoPackages(project));

    public static IEnumerable<string> CrossModule(ProjectModel project, string module, IReadOnlyCollection<string> modules)
    {
        foreach (var reference in project.ProjectReferences)
        {
            var owner = modules.FirstOrDefault(m => reference.StartsWith(m + ".", StringComparison.Ordinal));

            if (owner is not null && owner != module && reference != $"{owner}.Contracts")
            {
                yield return $"{project.Name}: project reference '{reference}' crosses into module '{owner}', only '{owner}.Contracts' is allowed";
            }
        }
    }

    public static IEnumerable<string> BuildingBlocksDomain(ProjectModel project) =>
        project.ProjectReferences.Select(r => $"{project.Name}: project reference '{r}' is not allowed")
            .Concat(NoPackages(project))
            .Concat(NoFrameworks(project));

    public static IEnumerable<string> BuildingBlocksApplication(ProjectModel project) =>
        References(project, ["BuildingBlocks.Domain"], [], allowOtherContracts: false)
            .Concat(NoPackages(project))
            .Concat(NoFrameworks(project));

    public static IEnumerable<string> BuildingBlocksApi(ProjectModel project) =>
        References(project, ["BuildingBlocks.Application"], [], allowOtherContracts: false);

    public static IEnumerable<string> Host(ProjectModel host, string registryText, IReadOnlyCollection<string> modules)
    {
        var begin = registryText.IndexOf("// modules:begin", StringComparison.Ordinal);
        var end = registryText.IndexOf("// modules:end", StringComparison.Ordinal);
        var region = begin >= 0 && end > begin ? registryText[begin..end] : null;

        if (region is null)
        {
            yield return "ModuleRegistry.cs: the '// modules:begin' and '// modules:end' markers are missing";
        }

        foreach (var module in modules)
        {
            if (!host.ProjectReferences.Contains($"{module}.Api"))
            {
                yield return $"{host.Name}: missing project reference '{module}.Api'";
            }

            if (region is not null && !region.Contains($"Add{module}Module(connectionString);", StringComparison.Ordinal))
            {
                yield return $"ModuleRegistry.cs: 'Add{module}Module(connectionString);' is missing between the markers";
            }
        }
    }

    private static IEnumerable<string> References(
        ProjectModel project,
        IReadOnlyCollection<string> allowed,
        IReadOnlyCollection<string> modules,
        bool allowOtherContracts) =>
        project.ProjectReferences
            .Where(r => !allowed.Contains(r) && !(allowOtherContracts && IsModuleContracts(r, modules)))
            .Select(r => $"{project.Name}: project reference '{r}' is not allowed");

    private static bool IsModuleContracts(string reference, IReadOnlyCollection<string> modules) =>
        reference.EndsWith(".Contracts", StringComparison.Ordinal)
        && modules.Contains(reference[..^".Contracts".Length]);

    private static IEnumerable<string> NoPackages(ProjectModel project) =>
        project.Packages.Select(p => $"{project.Name}: package '{p}' is not allowed");

    private static IEnumerable<string> NoFrameworks(ProjectModel project) =>
        project.FrameworkReferences.Select(f => $"{project.Name}: framework reference '{f}' is not allowed");
}
