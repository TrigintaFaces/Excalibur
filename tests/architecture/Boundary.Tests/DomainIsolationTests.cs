using NetArchTest.Rules;

using Shouldly;

using Xunit;

namespace Boundary.Tests;

/// <summary>
/// Validates Domain-Driven Design (DDD) isolation principles.
/// The domain layer must be pure business logic with zero infrastructure coupling.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class DomainIsolationTests
{
    /// <summary>
    /// CRITICAL RULE: Domain layer must be messaging-agnostic.
    /// Business logic should not know about messaging, events, or commands infrastructure.
    ///
    /// CRITICAL VIOLATION DETECTED:
    /// - Excalibur.Domain references Dispatch directly
    /// - Domain should not depend on ANY messaging framework
    /// </summary>
    [Fact]
    public void Domain_MustBe_MessagingAgnostic()
    {
        var domainAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Excalibur.Domain");

        _ = domainAssembly.ShouldNotBeNull(
            "The Excalibur.Domain assembly is not loaded — the module initializer force-loads it, so its " +
            "absence means drift, not a pass.");

        // Domain MAY reference Excalibur.Dispatch.Abstractions (for IDomainEvent/IIntegrationEvent); it must
        // not reference the concrete Excalibur.Dispatch (or .Patterns) implementation, nor any third-party
        // messaging framework. Assembly-identity, exact concrete-vs-Abstractions distinction.
        var thirdPartyMessaging = new[] { "MediatR", "MassTransit", "NServiceBus", "Rebus" };

        var violations = domainAssembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(n => n is not null
                        && ((n.StartsWith("Excalibur.Dispatch", StringComparison.Ordinal)
                             && !n.EndsWith(".Abstractions", StringComparison.Ordinal))
                            || thirdPartyMessaging.Any(f => n.StartsWith(f, StringComparison.Ordinal))))
            .ToList();

        violations.ShouldBeEmpty(
            "Domain layer must be messaging-agnostic per DDD principles — it may reference " +
            "Excalibur.Dispatch.Abstractions only, never the concrete Dispatch implementation or a third-party " +
            "messaging framework. Violations: " + string.Join(", ", violations));
    }

    /// <summary>
    /// Domain layer should not reference data access infrastructure.
    /// Repository abstractions may be defined in Domain, but implementations live in Data layer.
    /// </summary>
    [Fact]
    public void Domain_ShouldNotReference_DataProviders()
    {
        // Arrange
        var prohibitedDataDependencies = new[]
        {
            "Microsoft.Data.SqlClient",
            "Npgsql",
            "MongoDB.Driver",
            "StackExchange.Redis",
            "Elastic.Clients",
            "Dapper",
            "Microsoft.EntityFrameworkCore"
        };

        // Act
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Domain")
            .ShouldNot().HaveDependencyOnAny(prohibitedDataDependencies)
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue(
            "Domain must not reference data access providers. " +
            "Repository interfaces can live in Domain, but implementations belong in Data layer. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    /// <summary>
    /// Domain layer should not reference cloud provider SDKs.
    /// Cloud infrastructure belongs in provider packages, not domain logic.
    /// </summary>
    [Fact]
    public void Domain_ShouldNotReference_CloudProviders()
    {
        // Arrange
        var prohibitedCloudDependencies = new[]
        {
            "Azure",
            "AWSSDK",
            "Amazon.Lambda",
            "Google.Cloud",
            "Google.Apis"
        };

        // Act
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Domain")
            .ShouldNot().HaveDependencyOnAny(prohibitedCloudDependencies)
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue(
            "Domain must not reference cloud provider SDKs. " +
            "Cloud infrastructure belongs in provider packages. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    /// <summary>
    /// Domain layer should not reference serialization libraries (except for value object primitives).
    /// Serialization concerns belong in Application or Infrastructure layer.
    /// </summary>
    [Fact]
    public void Domain_ShouldNotReference_SerializationLibraries()
    {
        // Arrange
        var prohibitedSerializationDependencies = new[]
        {
            "Newtonsoft.Json",
            "MessagePack",
            "Google.Protobuf",
            "CloudNative.CloudEvents"
        };

        // Act
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Domain")
            .ShouldNot().HaveDependencyOnAny(prohibitedSerializationDependencies)
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue(
            "Domain should not reference serialization libraries. " +
            "System.Text.Json (BCL) may be acceptable for value objects, but avoid third-party serializers. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    /// <summary>
    /// Domain layer should not reference HTTP or web framework libraries.
    /// HTTP concerns belong in Web/API layer.
    /// </summary>
    [Fact]
    public void Domain_ShouldNotReference_WebFrameworks()
    {
        // Arrange
        var prohibitedWebDependencies = new[]
        {
            "Microsoft.AspNetCore",
            "Microsoft.Extensions.Http"
        };

        // Act
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Domain")
            .ShouldNot().HaveDependencyOnAny(prohibitedWebDependencies)
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue(
            "Domain must not reference web frameworks. " +
            "HTTP/Web concerns belong in Web/API layer. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    /// <summary>
    /// Domain entities and value objects should be immutable where possible.
    /// This test verifies that domain types don't expose public setters (DDD best practice).
    /// </summary>
    // REMOVED (bh0syy): Domain_ValueObjects_ShouldBeImmutable. Per SoftwareArchitect's ruling — dead
    // convention: no Excalibur.Domain type uses the `*ValueObject` suffix (this framework's value objects are
    // records, whose immutability the compiler already enforces), so the filter matched nothing and the guard
    // passed vacuously. There is no reliable structural marker to repoint to (a record is not necessarily a
    // value object). Deleted.

    /// <summary>
    /// Domain layer SHOULD only reference foundational libraries.
    /// Allowed: BCL, Microsoft.Extensions.*.Abstractions, minimal utilities.
    /// </summary>
    [Fact]
    public void Domain_ShouldOnlyReference_FoundationalLibraries()
    {
        // Arrange - These are the ONLY acceptable references for pure domain
        var allowedNamespaces = new[]
        {
            "System",
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.Logging.Abstractions",
            "Microsoft.Extensions.Options",
            "Ben.Demystifier", // Stack trace enhancement - acceptable
            "Medo.Uuid7" // UUID generation - acceptable for identifiers
        };

        var domainAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Excalibur.Domain");

        _ = domainAssembly.ShouldNotBeNull(
            "The Excalibur.Domain assembly is not loaded — the module initializer force-loads it, so its " +
            "absence means drift, not a pass.");

        var actualDependencies = domainAssembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(name => name is not null)
            .Distinct()
            .Where(name => !name!.StartsWith("System", StringComparison.Ordinal) &&
                           !name.StartsWith("mscorlib", StringComparison.Ordinal) &&
                           !name.StartsWith("netstandard", StringComparison.Ordinal) &&
                           !name.Equals("Excalibur.Domain", StringComparison.Ordinal) &&
                           // Domain may reference the foundational abstractions layers (per the dependency
                           // diagram: Domain -> {Data.Abstractions, Dispatch.Abstractions}).
                           !name.EndsWith(".Abstractions", StringComparison.Ordinal) &&
                           !allowedNamespaces.Any(allowed => name.StartsWith(allowed, StringComparison.Ordinal)))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        actualDependencies.ShouldBeEmpty(
            "Excalibur.Domain (pure DDD layer) must reference only the BCL, the allow-listed foundational " +
            "utilities, and *.Abstractions contract layers. Unexpected dependencies (each is either debt to " +
            "remove, or a legitimate foundational dependency to add to the allow-list with a justification): " +
            string.Join(", ", actualDependencies));
    }
}
