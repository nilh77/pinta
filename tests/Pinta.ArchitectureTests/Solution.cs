using System.Xml.Linq;

namespace Pinta.ArchitectureTests;

/// <summary>The projects under src/ and their declared ProjectReferences, read from the .csproj files.</summary>
public static class Solution
{
    public const string SharedKernel = "Pinta.SharedKernel";
    public const string Pricing = "Pinta.Pricing";
    public const string ModulePrefix = "Pinta.Modules.";
    public const string ContractsSuffix = ".Contracts";

    public static readonly string[] Modules =
        ["Identity", "Catalog", "Quoting", "Jobs", "Painters", "Payments", "Media", "Notifications", "Finance"];

    public static readonly string Root = FindRoot();

    public static readonly IReadOnlyDictionary<string, string[]> ProjectReferences = Directory
        .EnumerateFiles(Path.Combine(Root, "src"), "*.csproj", SearchOption.AllDirectories)
        .ToDictionary(
            path => Path.GetFileNameWithoutExtension(path),
            path => XDocument.Load(path).Descendants("ProjectReference")
                .Select(r => Path.GetFileNameWithoutExtension(r.Attribute("Include")!.Value.Replace('\\', '/')))
                .ToArray());

    public static bool IsContracts(string project) =>
        project.StartsWith(ModulePrefix, StringComparison.Ordinal) && project.EndsWith(ContractsSuffix, StringComparison.Ordinal);

    public static bool IsModule(string project) =>
        project.StartsWith(ModulePrefix, StringComparison.Ordinal) && !IsContracts(project);

    /// <summary>What a module may depend on: its own and other modules' Contracts, SharedKernel and Pricing (CLAUDE.md rule 3).</summary>
    public static bool ModuleMayReference(string reference) =>
        IsContracts(reference) || reference is SharedKernel or Pricing;

    /// <summary>Contracts are public surface: only SharedKernel, Pricing and other Contracts.</summary>
    public static bool ContractsMayReference(string reference) =>
        IsContracts(reference) || reference is SharedKernel or Pricing;

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Pinta.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Pinta.slnx not found above the test output.");
    }
}
