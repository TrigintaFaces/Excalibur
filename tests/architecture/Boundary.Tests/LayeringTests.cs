using System.Reflection;

using Shouldly;

using Xunit;

namespace Boundary.Tests;

/// <summary>
/// Validates architectural layering boundaries per eng/governance/package-map.yaml.
/// </summary>
/// <remarks>
/// These guards key on ASSEMBLY IDENTITY (assembly name + referenced-assembly names), never on CLR
/// namespace membership. ADR-075 dropped <c>.Abstractions</c> from namespaces, so a namespace-based guard
/// on <c>"Excalibur.Dispatch"</c> now over-captures the Abstractions project (which ships under that
/// namespace) and every <c>Excalibur.Dispatch.*</c> package. Assembly identity is immune to that. The
/// module initializer force-loads the full framework set, so an empty target set means drift, not a pass —
/// each guard asserts its set is non-empty (<c>ShouldNotBeEmpty</c>/<c>ShouldNotBeNull</c>).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class LayeringTests
{
    private static readonly string[] AwsGoogleSdks = ["AWSSDK", "Amazon.Lambda", "Google.Cloud", "Google.Apis"];
    private static readonly string[] AzureGoogleSdks = ["Azure.Messaging", "Azure.Storage", "Google.Cloud", "Google.Apis"];
    private static readonly string[] AzureAwsSdks = ["Azure.Messaging", "Azure.Storage", "AWSSDK", "Amazon.Lambda"];

    private static readonly string[] ProviderSdkPrefixes =
    [
        "Microsoft.Data.SqlClient", "Npgsql", "Azure.Messaging", "Azure.Storage", "Azure.Identity",
        "AWSSDK", "Amazon.Lambda", "Google.Cloud", "Google.Apis", "Confluent.Kafka", "RabbitMQ.Client",
        "MongoDB.Driver", "StackExchange.Redis", "Elastic.Clients"
    ];

    private static Assembly[] LoadedExcaliburAssemblies() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Excalibur", StringComparison.Ordinal) == true)
            .Where(a => a.GetName().Name?.Contains("Test", StringComparison.Ordinal) != true)
            .ToArray();

    /// <summary>
    /// TIER 1: <c>*.Abstractions</c> assemblies must not reference a CONCRETE Excalibur implementation
    /// assembly. An abstractions package MAY reference BCL, <c>Microsoft.Extensions.*.Abstractions</c>, the
    /// foundational layers <c>Excalibur.Domain</c> + <c>Excalibur.Data.Abstractions</c> +
    /// <c>Excalibur.Dispatch.Abstractions</c>, and other <c>*.Abstractions</c> packages.
    /// </summary>
    [Fact]
    public void Tier1_Abstractions_ShouldNotReference_ImplementationPackages()
    {
        // Foundational non-abstractions layer an abstractions package may depend on (per the dependency
        // diagram EventSourcing -> Domain -> {Data.Abstractions, Dispatch.Abstractions}). All *.Abstractions
        // targets are already permitted by the EndsWith(".Abstractions") check below.
        var allowedConcrete = new HashSet<string>(StringComparer.Ordinal) { "Excalibur.Domain" };

        // Two abstraction packages hold a VERIFIED, pre-existing dependency-inversion violation — a real
        // ProjectReference to the concrete Excalibur.Dispatch core — masked until now by the former vacuous
        // namespace guard. Fixing it needs investigation (removable ref vs a core type that must move into
        // Dispatch.Abstractions) and is out of this guard's scope. Pair-level exemption keeps full teeth: any
        // OTHER abstraction->implementation reference — including a new one from these two packages to a
        // different concrete — still fails.
        var trackedViolations = new HashSet<string>(StringComparer.Ordinal)
        {
            "Excalibur.Dispatch.Transport.Abstractions -> Excalibur.Dispatch",          // tracked: 0aofrb
            "Excalibur.Dispatch.Hosting.Serverless.Abstractions -> Excalibur.Dispatch", // tracked: 0aofrb
        };

        var abstractionAssemblies = LoadedExcaliburAssemblies()
            .Where(a => a.GetName().Name?.EndsWith(".Abstractions", StringComparison.Ordinal) == true)
            .ToArray();

        abstractionAssemblies.ShouldNotBeEmpty(
            "No Excalibur *.Abstractions assemblies are loaded — the module initializer force-loads the full " +
            "framework set, so an empty set means drift, not a pass.");

        var violations = new List<string>();
        foreach (var abs in abstractionAssemblies)
        {
            var name = abs.GetName().Name!;
            foreach (var refName in abs.GetReferencedAssemblies().Select(r => r.Name))
            {
                if (refName is null
                    || !refName.StartsWith("Excalibur", StringComparison.Ordinal)
                    || refName.EndsWith(".Abstractions", StringComparison.Ordinal)
                    || allowedConcrete.Contains(refName))
                {
                    continue;
                }

                var pair = $"{name} -> {refName}";
                if (!trackedViolations.Contains(pair))
                {
                    violations.Add(pair);
                }
            }
        }

        violations.ShouldBeEmpty(
            "Abstraction assemblies must reference only other abstractions, the foundational Excalibur.Domain " +
            "layer, and BCL/third-party contracts — never a concrete Excalibur implementation. Violations: " +
            string.Join("; ", violations));
    }

    /// <summary>
    /// TIER 2: the <c>Excalibur.Dispatch</c> core assembly must remain provider-agnostic — it must not
    /// reference any cloud or data provider SDK. Those belong in provider packages
    /// (<c>Excalibur.Dispatch.Transport.*</c>, <c>Excalibur.Data.*</c>).
    /// </summary>
    [Fact]
    public void Tier2_Dispatch_ShouldNotReference_ProviderSDKs()
    {
        var core = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Excalibur.Dispatch");

        _ = core.ShouldNotBeNull(
            "The Excalibur.Dispatch core assembly is not loaded — the module initializer force-loads it, so " +
            "its absence means drift, not a pass.");

        var violations = core.GetReferencedAssemblies()
            .Select(r => r.Name)
            .Where(n => n is not null && ProviderSdkPrefixes.Any(p => n.StartsWith(p, StringComparison.Ordinal)))
            .ToList();

        violations.ShouldBeEmpty(
            "Excalibur.Dispatch core must be provider-agnostic and reference no cloud/data provider SDK. " +
            "These belong in provider packages. Referenced SDKs: " + string.Join(", ", violations));
    }

    // REMOVED (bh0syy): Tier3_HostingPackages_MayReference_Implementation. Per SoftwareArchitect's ruling —
    // it asserted a PERMISSION ("hosting MAY reference the impl"), not a boundary, and a positive "may"
    // assertion both over-captures (non-messaging hosting packages legitimately don't reference the Dispatch
    // core) and enforces nothing. The real boundary is Tier1's contrapositive (abstractions must NOT
    // reference impl; hosting is not an abstraction, so it is unconstrained). There is no must-reference set
    // to enumerate. Same vacuous-permission class as the deleted HostingPackages_MayReference_Both.

    /// <summary>
    /// TIER 5: provider packages must only reference their own provider SDK (no cross-provider
    /// contamination). Package-unique namespaces and external-SDK targets, so a namespace filter cannot
    /// over-capture — kept as-is.
    /// </summary>
    [Fact]
    public void Tier5_AzureProvider_ShouldNotReference_AwsOrGoogleSDKs()
    {
        var result = NetArchTest.Rules.Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch.Transport.Azure")
            .ShouldNot().HaveDependencyOnAny(AwsGoogleSdks)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Azure provider must not reference AWS or Google SDKs. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Tier5_AwsProvider_ShouldNotReference_AzureOrGoogleSDKs()
    {
        var result = NetArchTest.Rules.Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch.Transport.Aws")
            .ShouldNot().HaveDependencyOnAny(AzureGoogleSdks)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "AWS provider must not reference Azure or Google SDKs. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Tier5_GoogleProvider_ShouldNotReference_AzureOrAwsSDKs()
    {
        var result = NetArchTest.Rules.Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch.Transport.Google")
            .ShouldNot().HaveDependencyOnAny(AzureAwsSdks)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Google provider must not reference Azure or AWS SDKs. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    // REMOVED (bh0syy): Tier6_ExcaliburPackages_ShouldPrefer_DispatchAbstractions_Over_Dispatch.
    // Per SoftwareArchitect's ruling: this was an aspirational namespace-based duplicate of the
    // authoritative assembly-identity boundary already enforced by the CLASS-1 guard
    // R17_8_Excalibur_MustOnlyReference_DispatchAbstractions (Phase8_3) — "Excalibur.* references
    // Excalibur.Dispatch.Abstractions only, not concrete Dispatch." A hard-wired-to-be-aspirational
    // namespace duplicate is a vacuity smell; the fixed CLASS-1 assembly guard is the authority.
}
