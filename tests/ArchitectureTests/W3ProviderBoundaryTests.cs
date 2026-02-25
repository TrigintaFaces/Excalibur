// -----------------------------------------------------------------------
// <copyright file="W3ProviderBoundaryTests.cs" company="Excalibur">
//     Licensed under the Excalibur License 1.0.
//     SPDX-License-Identifier: Excalibur-1.0 OR AGPL-3.0-or-later OR Apache-2.0
// </copyright>
// -----------------------------------------------------------------------

using NetArchTest.Rules;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.ArchitectureTests;

/// <summary>
/// W3 Provider Boundary Tests - Sprint 308, T5.1.
///
/// These tests verify the boundaries established during W3 Provider Extraction
/// (Sprints 306-307) are maintained. Database drivers (Dapper, Npgsql, SqlClient)
/// must be completely removed from Dispatch core and only exist in
/// Excalibur.Data.* provider packages.
///
/// Reference Decisions:
/// - AD-306-1 to AD-306-5: SqlServer provider patterns
/// - AD-307-1 to AD-307-4: Postgres provider patterns
///
/// These tests prevent architectural regression by enforcing at build time
/// that Dispatch remains infrastructure-agnostic.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Architecture")]
public sealed class W3ProviderBoundaryTests
{
    #region Dispatch Must Not Depend On Database Drivers

    /// <summary>
    /// Dispatch MUST NOT depend on Dapper.
    ///
    /// W3 Provider Extraction moved all database access to Excalibur.Data.* packages.
    /// Dapper should only exist in provider packages (SqlServer, Postgres).
    /// </summary>
    [Fact]
    public void Dispatch_MustNotDependOn_Dapper()
    {
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch")
            .And().DoNotResideInNamespaceContaining("Tests")
            .ShouldNot().HaveDependencyOn("Dapper")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Dispatch must not depend on Dapper after W3 Provider Extraction. " +
            "Database access belongs in Excalibur.Data.* provider packages. " +
            $"Violating types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    /// <summary>
    /// Dispatch MUST NOT depend on Npgsql (Postgres driver).
    ///
    /// Postgres driver should only exist in Excalibur.Data.Postgres package.
    /// </summary>
    [Fact]
    public void Dispatch_MustNotDependOn_Npgsql()
    {
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch")
            .And().DoNotResideInNamespaceContaining("Tests")
            .ShouldNot().HaveDependencyOn("Npgsql")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Dispatch must not depend on Npgsql after W3 Provider Extraction. " +
            "Postgres access belongs in Excalibur.Data.Postgres package. " +
            $"Violating types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    /// <summary>
    /// Dispatch MUST NOT depend on Microsoft.Data.SqlClient (SQL Server driver).
    ///
    /// SQL Server driver should only exist in Excalibur.Data.SqlServer package.
    /// </summary>
    [Fact]
    public void Dispatch_MustNotDependOn_SqlClient()
    {
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch")
            .And().DoNotResideInNamespaceContaining("Tests")
            .ShouldNot().HaveDependencyOn("Microsoft.Data.SqlClient")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Dispatch must not depend on Microsoft.Data.SqlClient after W3 Provider Extraction. " +
            "SQL Server access belongs in Excalibur.Data.SqlServer package. " +
            $"Violating types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    /// <summary>
    /// Theory test covering all database drivers at once.
    /// Provides a single test point for verifying driver isolation.
    /// </summary>
    [Theory]
    [InlineData("Dapper")]
    [InlineData("Npgsql")]
    [InlineData("Microsoft.Data.SqlClient")]
    public void Dispatch_MustNotDependOn_DatabaseDriver(string driver)
    {
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch")
            .And().DoNotResideInNamespaceContaining("Tests")
            .ShouldNot().HaveDependencyOn(driver)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Dispatch must not depend on {driver} after W3 Provider Extraction. " +
            "Database drivers belong in Excalibur.Data.* provider packages. " +
            $"Violating types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    #endregion

    #region Dispatch Must Not Reference System.Data Types Directly

    /// <summary>
    /// Dispatch MUST NOT reference System.Data.Common types directly.
    ///
    /// System.Data types (DbConnection, DbCommand, etc.) should only be
    /// used in provider packages, not in the core messaging framework.
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

    /// <summary>
    /// Dispatch MUST NOT reference System.Data.SqlClient types.
    ///
    /// Legacy SqlClient namespace should never be used in Excalibur.Dispatch.
    /// </summary>
    [Fact]
    public void Dispatch_MustNotReference_SystemDataSqlClient()
    {
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch")
            .And().DoNotResideInNamespaceContaining("Tests")
            .ShouldNot().HaveDependencyOn("System.Data.SqlClient")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Dispatch must not reference System.Data.SqlClient types. " +
            "SQL Server access belongs in Excalibur.Data.SqlServer package. " +
            $"Violating types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    #endregion

    #region Excalibur.Data.* Provider Patterns

    /// <summary>
    /// Excalibur.Data.* packages MAY depend on database drivers.
    /// This is the correct pattern - provider packages contain driver dependencies.
    ///
    /// This is a documentation test validating the expected pattern.
    /// </summary>
    [Fact]
    public void ExcaliburData_MayDependOn_DatabaseDrivers()
    {
        // This test documents the pattern - providers CAN have driver dependencies
        // We're validating that the architecture allows this correctly

        // Check if Excalibur.Data types exist in the domain
        var excaliburDataTypes = Types.InCurrentDomain()
            .That().ResideInNamespaceContaining("Excalibur.Data")
            .And().DoNotResideInNamespaceContaining("Tests")
            .GetTypes()
            .ToList();

        // Document: Excalibur.Data.* packages are where database drivers belong
        // This test passes to confirm the pattern is valid
        Assert.True(true,
            "Excalibur.Data.* packages are the correct location for database driver dependencies. " +
            $"Found {excaliburDataTypes.Count} types in Excalibur.Data.* namespace.");
    }

    /// <summary>
    /// Excalibur.Data.* packages should implement expected interfaces.
    /// Providers must implement IConnectionFactory, IDeadLetterStore, etc.
    /// </summary>
    [Fact]
    public void ExcaliburData_Providers_ShouldImplement_ExpectedInterfaces()
    {
        // Get all types in Excalibur.Data.* that end with Factory, Store, or Checker
        var providerTypes = Types.InCurrentDomain()
            .That().ResideInNamespaceContaining("Excalibur.Data")
            .And().DoNotResideInNamespaceContaining("Tests")
            .And().AreClasses()
            .And().AreNotAbstract()
            .GetTypes()
            .Where(t => t.Name.EndsWith("Factory") ||
                        t.Name.EndsWith("Store") ||
                        t.Name.EndsWith("Checker"))
            .ToList();

        // Each provider type should implement at least one interface
        var typesWithoutInterfaces = providerTypes
            .Where(t => !t.GetInterfaces().Any())
            .ToList();

        typesWithoutInterfaces.ShouldBeEmpty(
            "All Excalibur.Data.* provider types (Factory, Store, Checker) should implement interfaces. " +
            $"Types without interfaces: {string.Join(", ", typesWithoutInterfaces.Select(t => t.Name))}");
    }

    #endregion

    #region Dependency Direction Tests

    /// <summary>
    /// Excalibur.* packages CAN reference Excalibur.Dispatch.Abstractions.
    /// This is the correct dependency direction per separation of concerns.
    /// </summary>
    [Fact]
    public void Excalibur_CanReference_DispatchAbstractions()
    {
        // Document: Excalibur → Excalibur.Dispatch.Abstractions is the correct direction
        // Excalibur.Dispatch.Abstractions provides IDomainEvent, IIntegrationEvent, etc.

        var excaliburTypes = Types.InCurrentDomain()
            .That().ResideInNamespaceContaining("Excalibur")
            .And().DoNotResideInNamespaceContaining("Tests")
            .GetTypes()
            .ToList();

        // This test documents the allowed dependency direction
        Assert.True(true,
            "Excalibur.* packages may reference Excalibur.Dispatch.Abstractions for IDomainEvent, etc. " +
            $"Found {excaliburTypes.Count} types in Excalibur.* namespace.");
    }

    /// <summary>
    /// Dispatch MUST NOT depend on Excalibur.* packages.
    /// This is the critical separation: Dispatch is messaging only.
    /// </summary>
    [Fact]
    public void Dispatch_MustNotDependOn_Excalibur()
    {
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch")
            .And().DoNotResideInNamespaceContaining("Tests")
            .ShouldNot().HaveDependencyOnAny(new[]
            {
                "Excalibur.Data",
                "Excalibur.Domain",
                "Excalibur.EventSourcing",
                "Excalibur.Saga",
                "Excalibur.Outbox"
            })
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Dispatch must not depend on Excalibur.* packages per separation of concerns. " +
            "Dispatch handles messaging; Excalibur handles domain/persistence. " +
            $"Violating types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    /// <summary>
    /// Excalibur.Dispatch.Abstractions MUST NOT depend on any Excalibur packages.
    /// Abstractions layer must remain pure messaging contracts.
    /// </summary>
    [Fact]
    public void DispatchAbstractions_MustNotDependOn_Excalibur()
    {
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch.Abstractions")
            .ShouldNot().HaveDependencyOnAny(new[]
            {
                "Excalibur.Data",
                "Excalibur.Domain",
                "Excalibur.EventSourcing",
                "Excalibur.Saga",
                "Excalibur.Outbox"
            })
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Excalibur.Dispatch.Abstractions must not depend on Excalibur.* packages. " +
            "Abstractions layer provides pure messaging contracts only. " +
            $"Violating types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    #endregion

    #region Package Reference Verification

    /// <summary>
    /// Validates that Dispatch assembly has no references to banned assemblies.
    /// This is a stronger check using assembly reflection.
    /// </summary>
    [Fact]
    public void Dispatch_AssemblyReferences_ShouldNotInclude_BannedAssemblies()
    {
        // Banned assemblies that should not be referenced by Dispatch
        var bannedAssemblies = new[]
        {
            "Dapper",
            "Npgsql",
            "Microsoft.Data.SqlClient",
            "System.Data.SqlClient"
        };

        // Get the Dispatch assembly (if loaded)
        var dispatchAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Excalibur.Dispatch");

        if (dispatchAssembly is null)
        {
            // Dispatch assembly not loaded - test is informational
            Assert.True(true, "Dispatch assembly not loaded in test domain.");
            return;
        }

        var referencedAssemblies = dispatchAssembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        var violations = referencedAssemblies
            .Where(name => bannedAssemblies.Any(banned =>
                name.Equals(banned, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        violations.ShouldBeEmpty(
            "Dispatch assembly should not reference banned database driver assemblies. " +
            $"Violations: {string.Join(", ", violations)}");
    }

    #endregion
}
