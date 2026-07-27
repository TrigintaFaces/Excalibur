using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Shouldly;

using Xunit;

namespace Boundary.Tests.Architecture;

/// <summary>
/// Data provider packages must not couple to the CONCRETE <c>Excalibur.A3</c> implementation assembly; if a
/// provider needs A3 at all, it depends on <c>Excalibur.A3.Abstractions</c>.
/// </summary>
/// <remarks>
/// The former positive "every provider must reference A3.Abstractions" check was deleted — it is not a
/// boundary (a data provider needn't depend on the authorization/audit layer). This negative guard IS a
/// boundary and is kept. It enumerates the loaded <c>Excalibur.Data.*</c> provider assemblies dynamically
/// (self-maintaining), rather than the former stale hardcoded namespace list which loaded zero assemblies.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Architecture")]
public sealed class ProviderAbstractionsReferencesTests
{
    [Fact]
    public void Providers_Should_Not_Reference_A3_Implementation()
    {
        // Excalibur.Data.* provider assemblies (exclude the core Excalibur.Data, the .Abstractions layers,
        // and test assemblies). Enumerated from the loaded set, which the module initializer force-loads in
        // full — so this stays correct as providers are added or renamed.
        var providerAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name is { } n
                        && n.StartsWith("Excalibur.Data.", StringComparison.Ordinal)
                        && !n.EndsWith(".Abstractions", StringComparison.Ordinal)
                        && !n.Contains("Test", StringComparison.Ordinal))
            .ToArray();

        providerAssemblies.ShouldNotBeEmpty(
            "No Excalibur.Data.* provider assemblies are loaded — the module initializer force-loads the full " +
            "framework set, so an empty set means drift, not a pass.");

        var violations = new List<string>();
        foreach (var assembly in providerAssemblies)
        {
            // Allow Excalibur.A3.Abstractions; forbid the concrete Excalibur.A3 implementation assembly.
            var referencesConcreteA3 = assembly.GetReferencedAssemblies()
                .Any(a => a.Name is not null && a.Name.Equals("Excalibur.A3", StringComparison.Ordinal));

            if (referencesConcreteA3)
            {
                violations.Add(assembly.GetName().Name!);
            }
        }

        violations.ShouldBeEmpty(
            "Data provider assemblies must not reference the concrete Excalibur.A3 implementation assembly " +
            "(only Excalibur.A3.Abstractions is allowed). Violations: " + string.Join(", ", violations));
    }
}
