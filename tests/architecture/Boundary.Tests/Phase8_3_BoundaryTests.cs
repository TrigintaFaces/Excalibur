using System.Reflection;

using Shouldly;

using Xunit;

namespace Boundary.Tests;

/// <summary>
/// Phase 8.3: architecture boundary enforcement (R1.9 Dispatch⊥Excalibur, R17.8 Excalibur→Dispatch.Abstractions
/// only, R23.1 core⊥cloud-SDK).
/// </summary>
/// <remarks>
/// These guards key on ASSEMBLY IDENTITY (assembly name + referenced-assembly names), matched EXACTLY on the
/// concrete-vs-<c>.Abstractions</c> distinction — never on CLR namespace membership, which ADR-075 severed
/// from package identity. The module initializer force-loads the full framework set, so an empty target set
/// means drift; each guard asserts non-emptiness rather than skipping.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class Phase8_3_BoundaryTests
{
    #region R1.9: Dispatch MUST NOT Reference Excalibur

    /// <summary>
    /// R1.9: Excalibur.Dispatch.* assemblies MUST NOT reference the Excalibur application framework
    /// (Excalibur.* that is not itself Excalibur.Dispatch.*). Dispatch↔Dispatch references are allowed.
    /// </summary>
    [Fact]
    public void R1_9_Dispatch_MustNotReference_Excalibur()
    {
        var dispatchAssemblies = GetDispatchAssemblies();
        dispatchAssemblies.ShouldNotBeEmpty(
            "No Excalibur.Dispatch.* assemblies are loaded — the module initializer force-loads them, so an " +
            "empty set means drift, not a pass.");

        var violations = new List<string>();
        foreach (var assembly in dispatchAssemblies)
        {
            var excaliburRefs = assembly
                .GetReferencedAssemblies()
                .Select(a => a.Name)
                .Where(n => n is not null
                            && n.StartsWith("Excalibur", StringComparison.Ordinal)
                            && !n.StartsWith("Excalibur.Dispatch", StringComparison.Ordinal))
                .ToList();

            if (excaliburRefs.Count > 0)
            {
                violations.Add($"{assembly.GetName().Name} references: {string.Join(", ", excaliburRefs)}");
            }
        }

        violations.ShouldBeEmpty(
            "R1.9 VIOLATION: Dispatch must not reference the Excalibur application framework (Dispatch↔Dispatch " +
            "is allowed). Violations:\n" + string.Join("\n", violations));
    }

    // REMOVED (bh0syy): R1_9_DispatchTypes_MustNotDependOn_ExcaliburTypes — a namespace duplicate correctly
    // deleted (the R1.9 boundary is enforced by the assembly-identity guard above + the disk-based
    // ProjectReferenceTests.DispatchProjects_MustNotReference_ExcaliburProjects).

    #endregion

    #region R17.8: Excalibur MAY Reference Excalibur.Dispatch.Abstractions Only

    // REMOVED (bh0syy): R17_8_Excalibur_MustOnlyReference_DispatchAbstractions. Per SoftwareArchitect's
    // ruling — WRONG PREMISE. "Excalibur may reference ONLY Dispatch.Abstractions" is a dependency-inversion
    // ideal the framework deliberately does not follow: Excalibur → concrete Dispatch is the ALLOWED
    // direction (Outbox/Saga/Hosting/EventSourcing legitimately dispatch messages via the concrete
    // dispatcher — ~36 such references are the norm, not bugs). The dispatch-vs-excalibur separation rule
    // establishes exactly ONE hard boundary, Dispatch ⊥ Excalibur, enforced authoritatively by
    // R1_9_Dispatch_MustNotReference_Excalibur + the disk-based ProjectReferenceTests. Same wrong-premise
    // class as the deleted DispatchAbstractions_ShouldOnlyContain / R0_14.

    // REMOVED (bh0syy): R17_8_ExcaliburPatternsHosting_MustNotReference_DispatchPatterns. Per SoftwareArchitect's
    // ruling — dead target: Excalibur.Patterns.Hosting no longer exists (renamed/removed since Phase 8.3), so
    // the guard asserts against a non-existent assembly. Deleted.

    // REMOVED (bh0syy): R17_8_Excalibur_MayReference_DispatchAbstractions — informational only
    // (Console.WriteLine, asserts nothing); vacuous by construction.

    #endregion

    #region R23.1: Core MUST NOT Reference Cloud SDKs

    /// <summary>
    /// R23.1: the Excalibur.Dispatch and Excalibur.Dispatch.Patterns CORE assemblies must not reference cloud
    /// provider SDKs. Cloud SDKs belong in the provider packages only (pay-for-play).
    /// </summary>
    [Fact]
    public void R23_1_Dispatch_MustNotReference_CloudSDKs()
    {
        var cloudSdkPrefixes = new[] { "Azure", "AWSSDK", "Google.Cloud", "Microsoft.Azure" };

        // EXACT core assemblies — never the Transport.*/Hosting.* provider packages, which reference cloud
        // SDKs by design.
        var coreAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name is "Excalibur.Dispatch" or "Excalibur.Dispatch.Patterns")
            .ToArray();

        coreAssemblies.ShouldNotBeEmpty(
            "The Excalibur.Dispatch / .Patterns core assemblies are not loaded — the module initializer " +
            "force-loads them, so an empty set means drift, not a pass.");

        var violations = new List<string>();
        foreach (var assembly in coreAssemblies)
        {
            var cloudRefs = assembly
                .GetReferencedAssemblies()
                .Select(a => a.Name)
                .Where(n => n is not null && cloudSdkPrefixes.Any(p => n.StartsWith(p, StringComparison.Ordinal)))
                .ToList();

            if (cloudRefs.Count > 0)
            {
                violations.Add($"{assembly.GetName().Name} references: {string.Join(", ", cloudRefs)}");
            }
        }

        violations.ShouldBeEmpty(
            "R23.1 VIOLATION: the Dispatch core must not reference cloud provider SDKs (pay-for-play). Cloud " +
            "dependencies belong in Excalibur.Dispatch.Transport.<Provider> packages only. Violations:\n" +
            string.Join("\n", violations));
    }

    // REMOVED (bh0syy): R23_1_DispatchTypes_MustNotDependOn_CloudSDKs — namespace duplicate of the
    // assembly-identity guard above; post-rename it over-captures Excalibur-side types.

    /// <summary>
    /// R23.1: the Azure provider must only reference Azure SDKs (no AWS or Google).
    /// </summary>
    [Fact]
    public void R23_1_AzureProvider_MustNotReference_AwsOrGoogleSDKs()
    {
        var azureAssemblies = GetAssembliesMatching("Excalibur.Dispatch.Transport.Azure");
        azureAssemblies.ShouldNotBeEmpty(
            "The Azure transport assemblies are not loaded — the module initializer force-loads them, so an " +
            "empty set means drift, not a pass.");

        var violations = new List<string>();
        foreach (var assembly in azureAssemblies)
        {
            var invalidRefs = assembly
                .GetReferencedAssemblies()
                .Where(a => (a.Name?.StartsWith("AWSSDK", StringComparison.Ordinal) == true
                             || a.Name?.StartsWith("Google.Cloud", StringComparison.Ordinal) == true))
                .Where(a => a.Name?.Contains("Testing", StringComparison.Ordinal) != true)
                .Select(a => a.Name)
                .ToList();

            if (invalidRefs.Count > 0)
            {
                violations.Add($"{assembly.GetName().Name} references: {string.Join(", ", invalidRefs)}");
            }
        }

        violations.ShouldBeEmpty(
            "R23.1 VIOLATION: Azure provider packages must not reference AWS or Google SDKs. Violations:\n" +
            string.Join("\n", violations));
    }

    /// <summary>
    /// R23.1: the AWS provider must only reference AWS SDKs (no Azure or Google).
    /// </summary>
    [Fact]
    public void R23_1_AwsProvider_MustNotReference_AzureOrGoogleSDKs()
    {
        var awsAssemblies = GetAssembliesMatching(
            "Excalibur.Dispatch.Transport.Aws", "Excalibur.Dispatch.Hosting.Serverless.AwsLambda");
        awsAssemblies.ShouldNotBeEmpty(
            "The AWS provider assemblies are not loaded — the module initializer force-loads them, so an empty " +
            "set means drift, not a pass.");

        var violations = new List<string>();
        foreach (var assembly in awsAssemblies)
        {
            var invalidRefs = assembly
                .GetReferencedAssemblies()
                .Where(a => (a.Name?.StartsWith("Azure", StringComparison.Ordinal) == true
                             || a.Name?.StartsWith("Microsoft.Azure", StringComparison.Ordinal) == true
                             || a.Name?.StartsWith("Google.Cloud", StringComparison.Ordinal) == true))
                .Where(a => a.Name?.Contains("Testing", StringComparison.Ordinal) != true)
                .Select(a => a.Name)
                .ToList();

            if (invalidRefs.Count > 0)
            {
                violations.Add($"{assembly.GetName().Name} references: {string.Join(", ", invalidRefs)}");
            }
        }

        violations.ShouldBeEmpty(
            "R23.1 VIOLATION: AWS provider packages must not reference Azure or Google SDKs. Violations:\n" +
            string.Join("\n", violations));
    }

    /// <summary>
    /// R23.1: the Google provider must only reference Google SDKs (no Azure or AWS).
    /// </summary>
    [Fact]
    public void R23_1_GoogleProvider_MustNotReference_AzureOrAwsSDKs()
    {
        var googleAssemblies = GetAssembliesMatching(
            "Excalibur.Dispatch.Transport.Google", "Excalibur.Dispatch.Hosting.Serverless.GoogleCloudFunctions");
        googleAssemblies.ShouldNotBeEmpty(
            "The Google provider assemblies are not loaded — the module initializer force-loads them, so an " +
            "empty set means drift, not a pass.");

        var violations = new List<string>();
        foreach (var assembly in googleAssemblies)
        {
            var invalidRefs = assembly
                .GetReferencedAssemblies()
                .Where(a => (a.Name?.StartsWith("Azure", StringComparison.Ordinal) == true
                             || a.Name?.StartsWith("Microsoft.Azure", StringComparison.Ordinal) == true
                             || a.Name?.StartsWith("AWSSDK", StringComparison.Ordinal) == true))
                .Where(a => a.Name?.Contains("Testing", StringComparison.Ordinal) != true)
                .Select(a => a.Name)
                .ToList();

            if (invalidRefs.Count > 0)
            {
                violations.Add($"{assembly.GetName().Name} references: {string.Join(", ", invalidRefs)}");
            }
        }

        violations.ShouldBeEmpty(
            "R23.1 VIOLATION: Google provider packages must not reference Azure or AWS SDKs. Violations:\n" +
            string.Join("\n", violations));
    }

    #endregion

    #region R0.14: Serialization Boundary (obsoleted)

    // REMOVED (bh0syy): R0_14_Dispatch_MustOnlyUse_MemoryPack — obsoleted by ADR-295 (MemoryPack removed from
    // core; System.Text.Json is the default serializer, binary serializers are opt-in provider packages).
    // The "Dispatch must use MemoryPack only / no STJ in core" premise inverted; there is no MemoryPack-only
    // core to scope a guard to.

    // REMOVED (bh0syy): R0_14_PublicBoundaryPackages_MustUse_SystemTextJson — informational only
    // (Console.WriteLine, asserts nothing); vacuous by construction.

    #endregion

    #region Naming (out of bh0syy scope)

    // REMOVED (bh0syy): AllPublicTypes_ShouldFollow_MicrosoftNamingGuidelines — informational only (never
    // asserts) AND orthogonal to the boundary re-spec (it is a naming guard, not a dependency boundary).

    #endregion

    #region Helper Methods

    private static Assembly[] GetDispatchAssemblies()
    {
        // Metapackages (src/metapackages/*) are experience bundles that ProjectReference BOTH Dispatch and
        // Excalibur so a consumer gets everything with one reference — referencing both sides is their
        // purpose, so they are NOT "Dispatch messaging" packages subject to Dispatch ⊥ Excalibur. Their
        // "Excalibur.Dispatch.*" names would otherwise be captured by the prefix below. Exclude them,
        // enumerated from src/metapackages/ so the list stays correct as bundles are added/renamed.
        var metapackages = GetMetapackageAssemblyNames();

        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Excalibur.Dispatch", StringComparison.Ordinal) == true)
            .Where(a => a.GetName().Name?.Contains("Test", StringComparison.Ordinal) != true)
            .Where(a => !metapackages.Contains(a.GetName().Name!))
            .ToArray();
    }

    /// <summary>Assembly simple names of the experience metapackages under <c>src/metapackages/</c>.</summary>
    private static HashSet<string> GetMetapackageAssemblyNames()
    {
        var metaDir = Path.Combine(TestHelpers.GetRepositoryRoot(), "src", "metapackages");
        if (!Directory.Exists(metaDir))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return Directory
            .EnumerateFiles(metaDir, "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null)
            .ToHashSet(StringComparer.Ordinal)!;
    }

    private static Assembly[] GetAssembliesMatching(params string[] prefixes) =>
        AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => prefixes.Any(prefix => a.GetName().Name?.StartsWith(prefix, StringComparison.Ordinal) == true))
            .Where(a => a.GetName().Name?.Contains("Test", StringComparison.Ordinal) != true)
            .ToArray();

    #endregion
}
