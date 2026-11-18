using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace Laundry.Tests.Architecture;

public class PackageVersionTests
{
    private static readonly string[] EntityFrameworkAssemblies =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.EntityFrameworkCore.Relational",
    ];

    [Fact]
    public void HostOutputUsesOneEntityFrameworkCoreVersion()
    {
        var violations = CheckFolder(FindOutput(Path.Combine("src", "Host")));

        Assertions.NoViolations(violations);
    }

    [Fact]
    public void EveryProjectResolvesOneEntityFrameworkCoreVersion()
    {
        // Class libraries do not copy package assemblies to their output, so read the restore result instead.
        var violations = new List<string>();

        foreach (var assetsFile in Directory.GetFiles(BackendLayout.Root, "project.assets.json", SearchOption.AllDirectories))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(assetsFile));
            var resolved = document.RootElement.GetProperty("libraries").EnumerateObject()
                .Select(l => l.Name.Split('/'))
                .Where(parts => parts[0] is "Microsoft.EntityFrameworkCore" or "Microsoft.EntityFrameworkCore.Relational")
                .ToDictionary(parts => parts[0], parts => parts[1]);

            if (resolved.Values.Distinct().Count() > 1)
            {
                var versions = string.Join(", ", resolved.Select(v => $"{v.Key} {v.Value}"));
                violations.Add($"{Path.GetRelativePath(BackendLayout.Root, assetsFile)}: {versions}");
            }
        }

        Assertions.NoViolations(violations);
    }

    private static List<string> CheckFolder(string folder)
    {
        var versions = EntityFrameworkAssemblies
            .Where(name => File.Exists(Path.Combine(folder, name + ".dll")))
            .ToDictionary(name => name, name => ReadVersion(folder, name));

        return versions.Values.Distinct().Count() > 1
            ? [$"{folder}: {string.Join(", ", versions.Select(v => $"{v.Key} {v.Value}"))}"]
            : [];
    }

    // The assembly version stays 10.0.0.0 across patch releases, so compare the package (product) version.
    private static string ReadVersion(string folder, string assemblyName)
    {
        var info = FileVersionInfo.GetVersionInfo(Path.Combine(folder, assemblyName + ".dll"));
        var version = info.ProductVersion ?? info.FileVersion ?? string.Empty;

        return version.Split('+')[0];
    }

    private static string FindOutput(string projectRelativeFolder) =>
        OutputFolder(Path.Combine(BackendLayout.Root, projectRelativeFolder)) is var path && Directory.Exists(path)
            ? path
            : throw new DirectoryNotFoundException($"Build output was not found at {path}.");

    private static string OutputFolder(string projectFolder)
    {
        // The test output folder is <project>/bin/<configuration>/<framework>; every project uses the same layout.
        var framework = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));

        return Path.Combine(projectFolder, "bin", framework.Parent!.Name, framework.Name);
    }
}
