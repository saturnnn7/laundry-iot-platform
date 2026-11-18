namespace Laundry.Tests.Architecture;

public static class BackendLayout
{
    public static readonly string[] Layers = ["Contracts", "Domain", "Application", "Infrastructure", "Api"];

    public static readonly string[] RequiredModules = ["Identity", "Resources", "Telemetry", "Booking", "Maintenance"];

    public static string Root { get; } = FindRoot();

    public static IReadOnlyList<string> Modules { get; } = DiscoverModules();

    public static ProjectModel LoadModuleProject(string module, string layer) =>
        ProjectModel.Load(Path.Combine(Root, "src", "Modules", module, $"{module}.{layer}", $"{module}.{layer}.csproj"));

    public static ProjectModel LoadBuildingBlocksProject(string layer) =>
        ProjectModel.Load(Path.Combine(Root, "src", "BuildingBlocks", $"BuildingBlocks.{layer}", $"BuildingBlocks.{layer}.csproj"));

    public static ProjectModel LoadHost() =>
        ProjectModel.Load(Path.Combine(Root, "src", "Host", "Host.csproj"));

    public static string ReadModuleRegistry() =>
        File.ReadAllText(Path.Combine(Root, "src", "Host", "ModuleRegistry.cs"));

    public static IReadOnlyList<string> ExistingProjectNames(string module)
    {
        var moduleDirectory = Path.Combine(Root, "src", "Modules", module);

        return Directory.GetFiles(moduleDirectory, "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .ToList();
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Laundry.Backend.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Laundry.Backend.slnx was not found above the test output folder.");
    }

    private static List<string> DiscoverModules() =>
        Directory.GetDirectories(Path.Combine(Root, "src", "Modules"))
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToList();
}
