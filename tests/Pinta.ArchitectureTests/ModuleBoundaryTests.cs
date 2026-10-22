using System.Reflection;

namespace Pinta.ArchitectureTests;

/// <summary>
/// Module boundaries (CLAUDE.md, architecture rules 1–3 and 7). Two checks per rule: the declared
/// ProjectReferences (caught even if no type is used yet) and the references compiled into each assembly.
/// </summary>
public class ModuleBoundaryTests
{
    public static TheoryData<string> ModuleProjects => new(Solution.ProjectReferences.Keys.Where(Solution.IsModule).Order());
    public static TheoryData<string> ContractsProjects => new(Solution.ProjectReferences.Keys.Where(Solution.IsContracts).Order());

    [Fact]
    public void Every_module_has_a_contracts_and_a_private_project()
    {
        foreach (var module in Solution.Modules)
        {
            Assert.Contains($"{Solution.ModulePrefix}{module}", Solution.ProjectReferences.Keys);
            Assert.Contains($"{Solution.ModulePrefix}{module}{Solution.ContractsSuffix}", Solution.ProjectReferences.Keys);
        }
    }

    [Theory]
    [MemberData(nameof(ModuleProjects))]
    public void Module_project_references_only_contracts_shared_kernel_and_pricing(string module) =>
        AssertOnly(module, Solution.ProjectReferences[module], Solution.ModuleMayReference);

    [Theory]
    [MemberData(nameof(ModuleProjects))]
    public void Module_assembly_uses_only_contracts_shared_kernel_and_pricing(string module) =>
        AssertOnly(module, CompiledPintaReferences(module), Solution.ModuleMayReference);

    [Theory]
    [MemberData(nameof(ContractsProjects))]
    public void Contracts_project_references_only_contracts_shared_kernel_and_pricing(string contracts) =>
        AssertOnly(contracts, Solution.ProjectReferences[contracts], Solution.ContractsMayReference);

    [Theory]
    [MemberData(nameof(ContractsProjects))]
    public void Contracts_assembly_uses_only_contracts_shared_kernel_and_pricing(string contracts) =>
        AssertOnly(contracts, CompiledPintaReferences(contracts), Solution.ContractsMayReference);

    [Theory]
    [InlineData(Solution.SharedKernel)]
    [InlineData(Solution.Pricing)]
    public void Shared_kernel_and_pricing_reference_no_project(string project)
    {
        AssertOnly(project, Solution.ProjectReferences[project], _ => false);
        AssertOnly(project, CompiledPintaReferences(project), _ => false);
    }

    private static string[] CompiledPintaReferences(string assemblyName) =>
        Assembly.Load(assemblyName).GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => name.StartsWith("Pinta.", StringComparison.Ordinal))
            .ToArray();

    private static void AssertOnly(string project, IEnumerable<string> references, Func<string, bool> allowed)
    {
        var forbidden = references.Where(r => !allowed(r)).ToArray();
        Assert.True(forbidden.Length == 0,
            $"{project} references {string.Join(", ", forbidden)}. A module may only reference other modules' " +
            $".Contracts, {Solution.SharedKernel} and {Solution.Pricing}.");
    }
}
