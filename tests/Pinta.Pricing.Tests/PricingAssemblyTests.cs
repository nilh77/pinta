using System.Reflection;

namespace Pinta.Pricing.Tests;

// The golden cases TC01–TC13 arrive in H1; until then this guards rule 7 of CLAUDE.md.
public class PricingAssemblyTests
{
    [Fact]
    public void Pricing_depends_only_on_the_base_class_library()
    {
        var references = Assembly.Load("Pinta.Pricing").GetReferencedAssemblies().Select(a => a.Name!);

        Assert.All(references, name => Assert.True(
            name.StartsWith("System", StringComparison.Ordinal) || name == "netstandard",
            $"Pinta.Pricing must have no dependencies, but references {name}."));
    }
}
