using System.Reflection;

namespace Pinta.ArchitectureTests;

/// <summary>CLAUDE.md rule 7: Pinta.Pricing has no dependencies, no I/O and no clock.</summary>
public class PricingPurityTests
{
    private static readonly Assembly Pricing = Assembly.Load(Solution.Pricing);

    [Fact]
    public void Pricing_depends_only_on_the_base_class_library()
    {
        var references = Pricing.GetReferencedAssemblies().Select(a => a.Name!);

        Assert.All(references, name => Assert.True(
            name.StartsWith("System", StringComparison.Ordinal) || name == "netstandard",
            $"Pinta.Pricing must have no dependencies, but references {name}."));
    }

    [Theory]
    [InlineData("System.IO.")]
    [InlineData("System.Net.")]
    [InlineData("System.DateTime")]
    [InlineData("System.DateTimeOffset")]
    [InlineData("System.TimeProvider")]
    [InlineData("System.Random")]
    [InlineData("System.Double")]
    [InlineData("System.Single")]
    public void Pricing_types_do_not_expose_io_clock_randomness_or_floating_point(string forbiddenPrefix)
    {
        var used = Pricing.GetExportedTypes()
            .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .SelectMany(MemberTypes)
            .Select(t => t.FullName ?? t.Name)
            .Where(name => name.StartsWith(forbiddenPrefix, StringComparison.Ordinal))
            .Distinct()
            .ToArray();

        Assert.True(used.Length == 0, $"Pinta.Pricing exposes {string.Join(", ", used)}.");
    }

    private static IEnumerable<Type> MemberTypes(MemberInfo member) => member switch
    {
        PropertyInfo p => [p.PropertyType],
        FieldInfo f => [f.FieldType],
        MethodInfo m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType),
        _ => [],
    };
}
