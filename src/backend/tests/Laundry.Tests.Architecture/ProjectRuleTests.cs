using Xunit;

namespace Laundry.Tests.Architecture;

public class ProjectRuleTests
{
    private static IReadOnlyList<string> Modules => BackendLayout.Modules;

    private static IEnumerable<string> ForEachModule(Func<string, IEnumerable<string>> rule) =>
        Modules.SelectMany(rule);

    [Fact]
    public void AllRequiredModulesExist() =>
        Assertions.NoViolations(ProjectRules.RequiredModulesExist(Modules));

    [Fact]
    public void EveryModuleHasFiveProjects() =>
        Assertions.NoViolations(ForEachModule(m => ProjectRules.ModuleLayout(m, BackendLayout.ExistingProjectNames(m))));

    [Fact]
    public void DomainProjectsDependOnlyOnBuildingBlocksDomain() =>
        Assertions.NoViolations(ForEachModule(m =>
            ProjectRules.ModuleDomain(BackendLayout.LoadModuleProject(m, "Domain"), m)));

    [Fact]
    public void ContractsProjectsAreMinimal() =>
        Assertions.NoViolations(ForEachModule(m =>
            ProjectRules.ModuleContracts(BackendLayout.LoadModuleProject(m, "Contracts"), m)));

    [Fact]
    public void ApplicationProjectsFollowTheRules() =>
        Assertions.NoViolations(ForEachModule(m =>
            ProjectRules.ModuleApplication(BackendLayout.LoadModuleProject(m, "Application"), m, Modules)));

    [Fact]
    public void InfrastructureProjectsFollowTheRules() =>
        Assertions.NoViolations(ForEachModule(m =>
            ProjectRules.ModuleInfrastructure(BackendLayout.LoadModuleProject(m, "Infrastructure"), m, Modules)));

    [Fact]
    public void ApiProjectsFollowTheRules() =>
        Assertions.NoViolations(ForEachModule(m =>
            ProjectRules.ModuleApi(BackendLayout.LoadModuleProject(m, "Api"), m, Modules)));

    [Fact]
    public void ModulesReferenceOtherModulesOnlyThroughContracts() =>
        Assertions.NoViolations(ForEachModule(m => BackendLayout.Layers.SelectMany(layer =>
            ProjectRules.CrossModule(BackendLayout.LoadModuleProject(m, layer), m, Modules))));

    [Fact]
    public void BuildingBlocksStayLean()
    {
        Assertions.NoViolations(ProjectRules.BuildingBlocksDomain(BackendLayout.LoadBuildingBlocksProject("Domain")));
        Assertions.NoViolations(ProjectRules.BuildingBlocksApplication(BackendLayout.LoadBuildingBlocksProject("Application")));
        Assertions.NoViolations(ProjectRules.BuildingBlocksApi(BackendLayout.LoadBuildingBlocksProject("Api")));
    }

    [Fact]
    public void HostReferencesAndRegistersEveryModule() =>
        Assertions.NoViolations(ProjectRules.Host(BackendLayout.LoadHost(), BackendLayout.ReadModuleRegistry(), Modules));
}
