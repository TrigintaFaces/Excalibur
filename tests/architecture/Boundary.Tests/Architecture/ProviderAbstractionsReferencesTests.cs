using NetArchTest.Rules;
using System;

using Shouldly;

using Xunit;

namespace Boundary.Tests.Architecture;

/// <summary>
/// Report-only checks around provider packages referencing A3 abstractions and avoiding A3 implementation.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Architecture")]
public sealed class ProviderAbstractionsReferencesTests
{
    private static readonly string[] ProviderNamespaces = new[]
    {
        "Excalibur.Data.SqlServer",
        "Excalibur.Data.Postgres",
        "Excalibur.Data.Providers.MongoDB",
        "Excalibur.Data.Providers.Redis",
        "Excalibur.Data.ElasticSearch",
    };

    [Fact]
    public void Providers_Should_Prefer_A3_Abstractions()
    {
        foreach (var ns in ProviderNamespaces)
        {
            var hasAbstractions = Types.InCurrentDomain()
                .That().ResideInNamespace(ns)
                .Should().HaveDependencyOn("Excalibur.A3.Abstractions")
                .GetResult()
                .IsSuccessful;

            hasAbstractions.ShouldBeTrue($"Provider '{ns}' must reference Excalibur.A3.Abstractions.");
        }
    }

    [Fact]
    public void Providers_Should_Not_Reference_A3_Implementation()
    {
        foreach (var ns in ProviderNamespaces)
        {
            // Get assemblies for this provider namespace
            var assemblies = Types.InCurrentDomain()
                .That().ResideInNamespace(ns)
                .GetTypes()
                .Select(t => t.Assembly)
                .Distinct();

            foreach (var assembly in assemblies)
            {
                // Check assembly references for Excalibur.A3 (but allow Excalibur.A3.Abstractions)
                var a3References = assembly.GetReferencedAssemblies()
                    .Where(a => a.Name != null && a.Name.Equals("Excalibur.A3", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                a3References.Any().ShouldBeFalse(
                    $"Provider '{ns}' assembly '{assembly.GetName().Name}' references Excalibur.A3 implementation assembly. Only Excalibur.A3.Abstractions is allowed.");
            }
        }
    }
}
