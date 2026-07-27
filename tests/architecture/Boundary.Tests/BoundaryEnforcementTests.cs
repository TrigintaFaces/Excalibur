using NetArchTest.Rules;

using Shouldly;

using Xunit;

namespace Boundary.Tests;

/// <summary>
/// Enforces the Excalibur.Dispatch.Abstractions ↔ Excalibur.Dispatch architectural boundary — the
/// abstraction-layer pattern that enables provider composability and keeps concrete implementations out of
/// public APIs.
/// </summary>
/// <remarks>
/// These guards key on ASSEMBLY IDENTITY, not CLR namespace: post-ADR-075 the Abstractions project ships
/// under namespace <c>Excalibur.Dispatch</c>, so a namespace guard on that string over-captures. The
/// authoritative Dispatch⊥Excalibur project-reference boundaries live in the disk-based
/// <c>ProjectReferenceTests</c>; the namespace-duplicate guards that used to sit here were deleted rather
/// than kept broken. The module initializer force-loads the full framework set, so a null/empty target set
/// means drift — each surviving guard asserts non-emptiness rather than skipping.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class BoundaryEnforcementTests
{
    #region Core Boundary Rules

    // REMOVED (bh0syy): Dispatch_MustDependOn_DispatchAbstractions. Per SoftwareArchitect's ruling — a
    // low-value positive namespace check ("Dispatch classes should depend on Abstractions") that an
    // implementation depending on its own contracts trivially satisfies; it enforces no real boundary and
    // the namespace form over-captures post-ADR-075. Deleted.

    // REMOVED (bh0syy): DispatchAbstractions_MustNotDependOn_Dispatch. Per SoftwareArchitect's ruling —
    // a namespace-based duplicate that is unfixably broken post-ADR-075: the Abstractions assembly's types
    // now reside in CLR namespace "Excalibur.Dispatch", so HaveDependencyOn("Excalibur.Dispatch") prefix-
    // matches the Abstractions' OWN namespace and false-flags every type. The boundary is enforced
    // correctly and non-vacuously by the disk-based
    // ProjectReferenceTests.DispatchAbstractions_MustNotReference_DispatchCore.

    // REMOVED (bh0syy): DispatchAbstractions_ShouldOnlyContain_Interfaces_Abstracts_ValueTypes. Per
    // SoftwareArchitect's ruling — the premise ("Abstractions = only interfaces/abstracts/value-types") does
    // not match how a modern .NET abstractions package ships: positional records, DTOs, options, and result
    // types are legitimate contract surface that crosses the boundary, and this assembly holds ~100 of them
    // by design. The premise is unsalvageable, so the guard is deleted. The one real signal it carried —
    // genuine behavioral implementations (JsonEventSerializer / AotJsonEventSerializer / EventTypeRegistry)
    // living in the abstractions assembly — is a separate placement question tracked at d1lq95; if that
    // investigation says they should move, a correctly-scoped "no behavioral impls in Abstractions" guard is
    // added then.

    #endregion

    #region Excalibur Boundary Rules

    [Fact]
    public void ExcaliburPublicAPIs_MustNotExpose_DispatchTypes()
    {
        var excaliburNamespaces = new[]
        {
            "Excalibur.Application",
            "Excalibur.Data",
            "Excalibur.Domain",
            "Excalibur.Patterns",
            "Excalibur.Jobs",
            "Excalibur.Hosting",
            "Excalibur.A3"
        };

        // VERIFIED, pre-existing encapsulation violations (Excalibur.Data public types whose signatures
        // expose a concrete Excalibur.Dispatch type — e.g. ElasticsearchCircuitBreaker.State returns the
        // CircuitState enum from the concrete Dispatch assembly, not Dispatch.Abstractions). The likely fix
        // moves CircuitState + the circuit-breaker/dead-letter contract types into Dispatch.Abstractions —
        // a public-API change out of this guard's scope. Named exemption keeps the guard's teeth: any OTHER
        // (new) public type exposing a concrete Dispatch type still fails.
        var trackedExposures = new HashSet<string>(StringComparer.Ordinal)
        {
            "ElasticsearchCircuitBreaker",   // tracked: evrjug
            "IElasticsearchCircuitBreaker",  // tracked: evrjug
            "IOpenSearchCircuitBreaker",     // tracked: evrjug
            "PostgresDeadLetterStore",       // tracked: evrjug
            "SqlServerDeadLetterStore",      // tracked: evrjug
        };

        foreach (var ns in excaliburNamespaces)
        {
            var publicTypesExposingDispatch = Types.InCurrentDomain()
                .That().ResideInNamespace(ns)
                .And().ArePublic()
                .And().HaveDependencyOn("Excalibur.Dispatch")
                .GetTypes()
                .Where(t => ExposesDispatchInSignature(t))
                .Where(t => !trackedExposures.Contains(t.Name))
                .ToList();

            publicTypesExposingDispatch.ShouldBeEmpty(
                $"Excalibur.{ns} public APIs must not expose Excalibur.Dispatch types in method signatures or properties. " +
                "Use Excalibur.Dispatch.Abstractions interfaces instead for loose coupling. " +
                $"Types exposing Excalibur.Dispatch: {string.Join(", ", publicTypesExposingDispatch.Select(t => t.Name))}");
        }
    }

    // REMOVED (bh0syy): ExcaliburPackages_ShouldPrefer_DispatchAbstractions. Per SoftwareArchitect's ruling —
    // duplicate of Phase8_3.R17_8_Excalibur_MustOnlyReference_DispatchAbstractions (the assembly-identity
    // guard), and its namespace form over-captures post-ADR-075. Deleted.

    // REMOVED (bh0syy): ExcaliburDomain_MustNotDependOn_AnyDispatchPackage. Per SoftwareArchitect's ruling —
    // duplicate of the disk-based ProjectReferenceTests.ExcaliburDomain_MustNotReference_ConcreteDispatchProjects,
    // which enforces the same boundary correctly at the project-reference level (allowing the permitted
    // Excalibur.Dispatch.Abstractions). The namespace form here over-captures. Deleted.

    #endregion

    #region Dependency Direction Rules

    [Fact]
    public void DispatchAbstractions_ShouldOnlyDependOn_BCL_And_MSExtensionsAbstractions()
    {
        // Contract-layer allowed dependencies: BCL + the Microsoft.Extensions.*.Abstractions/Options
        // contract packages. Anything else is a dependency an abstractions package should not carry.
        var allowedNamespaces = new[]
        {
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.Logging.Abstractions",
            "Microsoft.Extensions.Options",

            // Medo.Uuid7: the UUIDv7 provider backing the contract-layer identifier value types
            // (CausationId / CorrelationId / EventId ordering). A deliberate, existing contract dependency,
            // not a new violation. It IS a removal candidate — .NET 9+ ships BCL Guid.CreateVersion7(), so
            // migrating off the third-party lib to the BCL is tracked at su72z9. The guard stays asserting,
            // so any NEW non-BCL dependency of the contract layer still fails.
            "Medo.Uuid7"
        };

        var abstractionsAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Excalibur.Dispatch.Abstractions");

        _ = abstractionsAssembly.ShouldNotBeNull(
            "Excalibur.Dispatch.Abstractions is not loaded — the module initializer force-loads it, so its " +
            "absence means drift, not a pass.");

        var actualDependencies = abstractionsAssembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Distinct()
            .Where(name => !name.StartsWith("System", StringComparison.Ordinal) &&
                           !name.StartsWith("mscorlib", StringComparison.Ordinal) &&
                           !name.StartsWith("netstandard", StringComparison.Ordinal) &&
                           !allowedNamespaces.Any(allowed => name.StartsWith(allowed, StringComparison.Ordinal)))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        actualDependencies.ShouldBeEmpty(
            "Excalibur.Dispatch.Abstractions (the contract layer) must depend only on the BCL and the " +
            "Microsoft.Extensions.*.Abstractions/Options contract packages. Unexpected dependencies (each is " +
            "either debt to remove, or a legitimate contract dependency to add to the allow-list with a " +
            "justification): " + string.Join(", ", actualDependencies));
    }

    // REMOVED (bh0syy): HostingPackages_MayReference_Both_AbstractionsAndDispatch. Per SoftwareArchitect's
    // ruling — an informational "permission" assertion ("hosting MAY reference the impl"), not a boundary;
    // it enforces nothing. Same class as the deleted Tier3. Deleted.

    #endregion

    #region Interface Implementation Rules

    // REMOVED (bh0syy): Dispatch_PublicClasses_ShouldImplement_DispatchAbstractionsInterfaces. Per
    // SoftwareArchitect's ruling — a "≥90% of public classes implement an Abstractions interface" metric is
    // an arbitrary heuristic, not a boundary: it is provably false against the real type set (DTOs, records,
    // options, and enums legitimately implement nothing), and it was passing only vacuously on a partial
    // type set. Same non-boundary class as the deleted Tier3 permission test. Deleted.

    // REMOVED (bh0syy): DependencyInjection_ShouldRegister_Interfaces_Not_ConcreteTypes. Per SoftwareArchitect's
    // ruling — informational only (ends in `true.ShouldBeTrue`, asserts nothing about the actual
    // registrations); vacuous by construction. Deleted.

    #endregion

    #region Helper Methods

    private static bool ExposesDispatchInSignature(Type type)
    {
        var methodsExposingDispatch = type.GetMethods()
            .Where(m => m.IsPublic && !m.IsSpecialName)
            .Any(m => IsDispatchType(m.ReturnType) ||
                      m.GetParameters().Any(p => IsDispatchType(p.ParameterType)));

        var propertiesExposingDispatch = type.GetProperties()
            .Where(p => p.GetMethod?.IsPublic == true || p.SetMethod?.IsPublic == true)
            .Any(p => IsDispatchType(p.PropertyType));

        var constructorsExposingDispatch = type.GetConstructors()
            .Where(c => c.IsPublic)
            .Any(c => c.GetParameters().Any(p => IsDispatchType(p.ParameterType)));

        return methodsExposingDispatch || propertiesExposingDispatch || constructorsExposingDispatch;
    }

    private static bool IsDispatchType(Type type)
    {
        var typeToCheck = type.IsGenericType ? type.GetGenericTypeDefinition() : type;

        // Distinguish Dispatch IMPLEMENTATION types from Abstractions types by assembly name (both share
        // the CLR namespace "Excalibur.Dispatch" post-ADR-075).
        return typeToCheck.Namespace?.StartsWith("Excalibur.Dispatch", StringComparison.Ordinal) == true &&
               typeToCheck.Assembly.GetName().Name != "Excalibur.Dispatch.Abstractions";
    }

    #endregion
}
