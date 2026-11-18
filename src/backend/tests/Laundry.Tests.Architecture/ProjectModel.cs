using System.Xml.Linq;

namespace Laundry.Tests.Architecture;

public sealed record ProjectModel(
    string Name,
    IReadOnlyCollection<string> ProjectReferences,
    IReadOnlyCollection<string> Packages,
    IReadOnlyCollection<string> FrameworkReferences)
{
    public static ProjectModel Load(string csprojPath)
    {
        var document = XDocument.Load(csprojPath);

        return new ProjectModel(
            Path.GetFileNameWithoutExtension(csprojPath),
            Includes(document, "ProjectReference").Select(ReferenceName).ToList(),
            Includes(document, "PackageReference").ToList(),
            Includes(document, "FrameworkReference").ToList());
    }

    private static IEnumerable<string> Includes(XDocument document, string element) =>
        document.Descendants(element)
            .Select(e => (string?)e.Attribute("Include"))
            .OfType<string>();

    private static string ReferenceName(string include)
    {
        var fileName = include.Replace('\\', '/').Split('/')[^1];
        return fileName.EndsWith(".csproj", StringComparison.Ordinal) ? fileName[..^".csproj".Length] : fileName;
    }
}
