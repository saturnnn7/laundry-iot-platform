using Xunit;

namespace Laundry.Tests.Architecture;

public class RuleFunctionTests
{
    private static readonly string[] Modules = ["Alpha", "Beta"];

    private static ProjectModel Project(
        string name,
        string[]? references = null,
        string[]? packages = null,
        string[]? frameworks = null) =>
        new(name, references ?? [], packages ?? [], frameworks ?? []);

    private static void AssertViolation(IEnumerable<string> violations, string project, string offender)
    {
        var list = violations.ToList();

        Assert.Contains(list, v => v.Contains(project, StringComparison.Ordinal) && v.Contains(offender, StringComparison.Ordinal));
    }

    [Fact]
    public void MissingModuleIsReported()
    {
        AssertViolation(ProjectRules.RequiredModulesExist(["Identity"]), "Booking", "missing");
    }

    [Fact]
    public void MissingLayerProjectIsReported()
    {
        AssertViolation(ProjectRules.ModuleLayout("Alpha", ["Alpha.Domain"]), "Alpha.Api", "missing");
    }

    [Fact]
    public void DomainRules()
    {
        Assert.Empty(ProjectRules.ModuleDomain(Project("Alpha.Domain", ["BuildingBlocks.Domain"]), "Alpha"));

        AssertViolation(
            ProjectRules.ModuleDomain(Project("Alpha.Domain", ["BuildingBlocks.Domain", "Alpha.Application"]), "Alpha"),
            "Alpha.Domain",
            "Alpha.Application");
        AssertViolation(
            ProjectRules.ModuleDomain(Project("Alpha.Domain", ["BuildingBlocks.Domain"], ["Newtonsoft.Json"]), "Alpha"),
            "Alpha.Domain",
            "Newtonsoft.Json");
        AssertViolation(
            ProjectRules.ModuleDomain(Project("Alpha.Domain", ["BuildingBlocks.Domain"], frameworks: ["Microsoft.AspNetCore.App"]), "Alpha"),
            "Alpha.Domain",
            "Microsoft.AspNetCore.App");
        AssertViolation(ProjectRules.ModuleDomain(Project("Alpha.Domain"), "Alpha"), "Alpha.Domain", "BuildingBlocks.Domain");
    }

    [Fact]
    public void ContractsRules()
    {
        Assert.Empty(ProjectRules.ModuleContracts(Project("Alpha.Contracts", ["BuildingBlocks.Application"]), "Alpha"));

        AssertViolation(
            ProjectRules.ModuleContracts(Project("Alpha.Contracts", ["Alpha.Domain"]), "Alpha"),
            "Alpha.Contracts",
            "Alpha.Domain");
        AssertViolation(
            ProjectRules.ModuleContracts(Project("Alpha.Contracts", packages: ["Some.Package"]), "Alpha"),
            "Alpha.Contracts",
            "Some.Package");
        AssertViolation(
            ProjectRules.ModuleContracts(Project("Alpha.Contracts", frameworks: ["Microsoft.AspNetCore.App"]), "Alpha"),
            "Alpha.Contracts",
            "Microsoft.AspNetCore.App");
    }

    [Fact]
    public void ApplicationRules()
    {
        var good = Project(
            "Alpha.Application",
            ["Alpha.Domain", "Alpha.Contracts", "Beta.Contracts", "BuildingBlocks.Application"],
            [ProjectRules.DependencyInjectionAbstractions]);
        Assert.Empty(ProjectRules.ModuleApplication(good, "Alpha", Modules));

        AssertViolation(
            ProjectRules.ModuleApplication(Project("Alpha.Application", ["Beta.Domain"]), "Alpha", Modules),
            "Alpha.Application",
            "Beta.Domain");
        AssertViolation(
            ProjectRules.ModuleApplication(Project("Alpha.Application", ["Alpha.Infrastructure"]), "Alpha", Modules),
            "Alpha.Application",
            "Alpha.Infrastructure");
        AssertViolation(
            ProjectRules.ModuleApplication(Project("Alpha.Application", packages: ["Microsoft.EntityFrameworkCore"]), "Alpha", Modules),
            "Alpha.Application",
            "Microsoft.EntityFrameworkCore");
        AssertViolation(
            ProjectRules.ModuleApplication(Project("Alpha.Application", frameworks: ["Microsoft.AspNetCore.App"]), "Alpha", Modules),
            "Alpha.Application",
            "Microsoft.AspNetCore.App");
    }

    [Fact]
    public void InfrastructureRules()
    {
        var good = Project(
            "Alpha.Infrastructure",
            ["Alpha.Application", "Alpha.Domain", "BuildingBlocks.Infrastructure", "Beta.Contracts"]);
        Assert.Empty(ProjectRules.ModuleInfrastructure(good, "Alpha", Modules));

        AssertViolation(
            ProjectRules.ModuleInfrastructure(Project("Alpha.Infrastructure", ["Alpha.Api"]), "Alpha", Modules),
            "Alpha.Infrastructure",
            "Alpha.Api");
        AssertViolation(
            ProjectRules.ModuleInfrastructure(Project("Alpha.Infrastructure", ["Beta.Infrastructure"]), "Alpha", Modules),
            "Alpha.Infrastructure",
            "Beta.Infrastructure");
        AssertViolation(
            ProjectRules.ModuleInfrastructure(Project("Alpha.Infrastructure", packages: ["Npgsql"]), "Alpha", Modules),
            "Alpha.Infrastructure",
            "Npgsql");
        AssertViolation(
            ProjectRules.ModuleInfrastructure(Project("Alpha.Infrastructure", frameworks: ["Microsoft.AspNetCore.App"]), "Alpha", Modules),
            "Alpha.Infrastructure",
            "Microsoft.AspNetCore.App");
    }

    [Fact]
    public void OnlyIdentityInfrastructureMayUseTheIdentityStorePackage()
    {
        const string package = "Microsoft.AspNetCore.Identity.EntityFrameworkCore";

        Assert.Empty(ProjectRules.ModuleInfrastructure(
            Project("Identity.Infrastructure", packages: [package]), "Identity", ["Identity", "Beta"]));
        AssertViolation(
            ProjectRules.ModuleInfrastructure(Project("Alpha.Infrastructure", packages: [package]), "Alpha", Modules),
            "Alpha.Infrastructure",
            package);
        AssertViolation(
            ProjectRules.ModuleInfrastructure(
                Project("Identity.Infrastructure", packages: [package, "Npgsql"]), "Identity", ["Identity", "Beta"]),
            "Identity.Infrastructure",
            "Npgsql");
    }

    [Fact]
    public void ApiRules()
    {
        var good = Project(
            "Alpha.Api",
            ["Alpha.Application", "Alpha.Infrastructure", "BuildingBlocks.Api", "Beta.Contracts"]);
        Assert.Empty(ProjectRules.ModuleApi(good, "Alpha", Modules));

        AssertViolation(
            ProjectRules.ModuleApi(Project("Alpha.Api", ["Alpha.Domain"]), "Alpha", Modules),
            "Alpha.Api",
            "Alpha.Domain");
        AssertViolation(
            ProjectRules.ModuleApi(Project("Alpha.Api", ["Beta.Api"]), "Alpha", Modules),
            "Alpha.Api",
            "Beta.Api");
        AssertViolation(
            ProjectRules.ModuleApi(Project("Alpha.Api", packages: ["Swashbuckle.AspNetCore"]), "Alpha", Modules),
            "Alpha.Api",
            "Swashbuckle.AspNetCore");
    }

    [Fact]
    public void CrossModuleRule()
    {
        Assert.Empty(ProjectRules.CrossModule(Project("Alpha.Api", ["Alpha.Application", "Beta.Contracts"]), "Alpha", Modules));

        AssertViolation(
            ProjectRules.CrossModule(Project("Alpha.Application", ["Beta.Domain"]), "Alpha", Modules),
            "Alpha.Application",
            "Beta.Domain");
    }

    [Fact]
    public void BuildingBlocksRules()
    {
        Assert.Empty(ProjectRules.BuildingBlocksDomain(Project("BuildingBlocks.Domain")));
        Assert.Empty(ProjectRules.BuildingBlocksApplication(Project("BuildingBlocks.Application", ["BuildingBlocks.Domain"])));
        Assert.Empty(ProjectRules.BuildingBlocksApi(Project("BuildingBlocks.Api", ["BuildingBlocks.Application"])));

        AssertViolation(
            ProjectRules.BuildingBlocksDomain(Project("BuildingBlocks.Domain", ["BuildingBlocks.Application"])),
            "BuildingBlocks.Domain",
            "BuildingBlocks.Application");
        AssertViolation(
            ProjectRules.BuildingBlocksDomain(Project("BuildingBlocks.Domain", packages: ["Some.Package"])),
            "BuildingBlocks.Domain",
            "Some.Package");
        AssertViolation(
            ProjectRules.BuildingBlocksApplication(Project("BuildingBlocks.Application", ["BuildingBlocks.Domain", "BuildingBlocks.Infrastructure"])),
            "BuildingBlocks.Application",
            "BuildingBlocks.Infrastructure");
        AssertViolation(
            ProjectRules.BuildingBlocksApplication(Project("BuildingBlocks.Application", frameworks: ["Microsoft.AspNetCore.App"])),
            "BuildingBlocks.Application",
            "Microsoft.AspNetCore.App");
        AssertViolation(
            ProjectRules.BuildingBlocksApi(Project("BuildingBlocks.Api", ["BuildingBlocks.Application", "BuildingBlocks.Domain"])),
            "BuildingBlocks.Api",
            "BuildingBlocks.Domain");
    }

    [Fact]
    public void HostRules()
    {
        const string goodRegistry = "// modules:begin\nservices.AddAlphaModule(connectionString);\nservices.AddBetaModule(connectionString);\n// modules:end";
        var goodHost = Project("Host", ["Alpha.Api", "Beta.Api"]);
        Assert.Empty(ProjectRules.Host(goodHost, goodRegistry, Modules));

        AssertViolation(ProjectRules.Host(Project("Host", ["Alpha.Api"]), goodRegistry, Modules), "Host", "Beta.Api");
        AssertViolation(
            ProjectRules.Host(goodHost, "// modules:begin\nservices.AddAlphaModule(connectionString);\n// modules:end", Modules),
            "ModuleRegistry.cs",
            "AddBetaModule");
        AssertViolation(
            ProjectRules.Host(goodHost, "services.AddAlphaModule(connectionString);\nservices.AddBetaModule(connectionString);", Modules),
            "ModuleRegistry.cs",
            "markers");
        AssertViolation(
            ProjectRules.Host(
                goodHost,
                "services.AddBetaModule(connectionString);\n// modules:begin\nservices.AddAlphaModule(connectionString);\n// modules:end",
                Modules),
            "ModuleRegistry.cs",
            "AddBetaModule");
    }

    [Fact]
    public void AssemblyReferenceRules()
    {
        Assert.Empty(AssemblyRules.References("Laundry.Alpha.Domain", ["System.Runtime", "Laundry.BuildingBlocks.Domain"], Modules));
        Assert.Empty(AssemblyRules.References("Laundry.Alpha.Api", ["Laundry.Beta.Contracts"], Modules));

        AssertViolation(
            AssemblyRules.References("Laundry.Alpha.Api", ["Laundry.Beta.Domain"], Modules),
            "Laundry.Alpha.Api",
            "Laundry.Beta.Domain");
        AssertViolation(
            AssemblyRules.References("Laundry.Alpha.Domain", ["Microsoft.EntityFrameworkCore"], Modules),
            "Laundry.Alpha.Domain",
            "Microsoft.EntityFrameworkCore");
        AssertViolation(
            AssemblyRules.References("Laundry.Alpha.Application", ["Microsoft.AspNetCore.Http"], Modules),
            "Laundry.Alpha.Application",
            "Microsoft.AspNetCore.Http");
    }

    [Fact]
    public void DbContextRules()
    {
        var good = new DbContextInfo("AlphaDbContext", "Laundry.Alpha.Infrastructure", "alpha");
        Assert.Empty(AssemblyRules.DbContexts("Alpha", [good]));

        AssertViolation(AssemblyRules.DbContexts("Alpha", []), "Alpha", "found 0");
        AssertViolation(AssemblyRules.DbContexts("Alpha", [good, good]), "Alpha", "found 2");
        AssertViolation(
            AssemblyRules.DbContexts("Alpha", [good with { TypeName = "Other" }]),
            "Alpha",
            "Other");
        AssertViolation(
            AssemblyRules.DbContexts("Alpha", [good with { Namespace = "Laundry.Alpha.Api" }]),
            "Alpha",
            "Laundry.Alpha.Api");
        AssertViolation(
            AssemblyRules.DbContexts("Alpha", [good with { SchemaName = "public" }]),
            "Alpha",
            "public");
        AssertViolation(
            AssemblyRules.DistinctSchemas([("Alpha", "shared"), ("Beta", "shared")]),
            "shared",
            "Beta");
    }
}
