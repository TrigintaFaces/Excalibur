// -----------------------------------------------------------------------
// <copyright file="W3ProviderBoundaryTests.cs" company="Excalibur">
//     Licensed under the Excalibur License 1.0.
//     SPDX-License-Identifier: Excalibur-1.0 OR AGPL-3.0-or-later OR Apache-2.0
// </copyright>
// -----------------------------------------------------------------------

using NetArchTest.Rules;

using Shouldly;

using Xunit;

namespace Boundary.Tests;

/// <summary>
/// W3 Provider Boundary Tests — database drivers (Dapper, Npgsql, SqlClient) must not leak into the
/// Dispatch core; they belong only in Excalibur.Data.* provider packages.
/// </summary>
/// <remarks>
/// The namespace-based <c>HaveDependencyOn(driver)</c> forms of these checks were deleted: post-ADR-075
/// <c>ResideInNamespace("Excalibur.Dispatch")</c> over-captures the Abstractions project and Excalibur.Outbox
/// types shipping under <c>Excalibur.Dispatch.Delivery</c>, and the same boundary is enforced authoritatively
/// and non-vacuously by the assembly-identity <see cref="Dispatch_AssemblyReferences_ShouldNotInclude_BannedAssemblies"/>
/// below plus the disk-based <c>ProjectReferenceTests</c> / <c>PackageReferenceTests</c>.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Architecture")]
public sealed class W3ProviderBoundaryTests
{
    // REMOVED (bh0syy) — namespace-based duplicates of the assembly-identity banned-assembly guard below
    // (+ the disk-based ProjectReferenceTests/PackageReferenceTests), all over-capturing post-ADR-075:
    //   Dispatch_MustNotDependOn_Dapper / _Npgsql / _SqlClient, Dispatch_MustNotDependOn_DatabaseDriver
    //   (Theory), Dispatch_MustNotReference_SystemDataSqlClient, Dispatch_MustNotDependOn_Excalibur,
    //   DispatchAbstractions_MustNotDependOn_Excalibur.
    // REMOVED (bh0syy) — informational (`true.ShouldBeTrue`, assert nothing): ExcaliburData_MayDependOn_DatabaseDrivers,
    //   Excalibur_CanReference_DispatchAbstractions.

    #region Dispatch Must Not Reference System.Data Types Directly

    /// <summary>
    /// Dispatch MUST NOT reference System.Data.Common types directly — database operations belong in
    /// Excalibur.Data.* provider packages. Kept as a namespace/type check: the target is a shared BCL
    /// namespace, which has no assembly-identity equivalent.
    /// </summary>
    [Fact]
    public void Dispatch_MustNotReference_SystemDataCommon()
    {
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch")
            .And().DoNotResideInNamespaceContaining("Tests")
            .ShouldNot().HaveDependencyOn("System.Data.Common")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Dispatch must not reference System.Data.Common types. " +
            "Database operations belong in Excalibur.Data.* provider packages. " +
            $"Violating types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    #endregion

    // REMOVED (bh0syy): ExcaliburData_Providers_ShouldImplement_ExpectedInterfaces. Per SoftwareArchitect's
    // ruling — a design heuristic ("every *Factory/*Store/*Checker implements an interface"), not a
    // dependency boundary; it is legitimately violated by types like SqlServerBulkOperationFactory that need
    // no interface. Deleted.

    #region Package Reference Verification

    /// <summary>
    /// The <c>Excalibur.Dispatch</c> core assembly must not reference any banned database-driver assembly.
    /// Assembly-identity check (exact match) — the authoritative form of the driver-isolation boundary.
    /// </summary>
    [Fact]
    public void Dispatch_AssemblyReferences_ShouldNotInclude_BannedAssemblies()
    {
        var bannedAssemblies = new[]
        {
            "Dapper",
            "Npgsql",
            "Microsoft.Data.SqlClient",
            "System.Data.SqlClient"
        };

        var dispatchAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Excalibur.Dispatch");

        _ = dispatchAssembly.ShouldNotBeNull(
            "The Excalibur.Dispatch core assembly is not loaded — the module initializer force-loads it, so " +
            "its absence means drift, not a pass.");

        var violations = dispatchAssembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(name => bannedAssemblies.Any(banned =>
                name.Equals(banned, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        violations.ShouldBeEmpty(
            "Dispatch assembly should not reference banned database driver assemblies. " +
            $"Violations: {string.Join(", ", violations)}");
    }

    #endregion
}
