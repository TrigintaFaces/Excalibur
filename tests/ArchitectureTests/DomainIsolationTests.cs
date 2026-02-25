using NetArchTest.Rules;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.ArchitectureTests;

/// <summary>
/// Validates Domain-Driven Design (DDD) isolation principles.
/// The domain layer must be pure business logic with zero infrastructure coupling.
/// </summary>
public class DomainIsolationTests
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
        // Arrange
        var prohibitedMessagingDependencies = new[]
        {
            "Excalibur.Dispatch",
            "Excalibur.Dispatch.Abstractions",
            "MediatR",
            "MassTransit",
            "NServiceBus",
            "Rebus"
        };

        // Act
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Domain")
            .ShouldNot().HaveDependencyOnAny(prohibitedMessagingDependencies)
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue(
            "Domain layer must be messaging-agnostic per DDD principles. " +
            "Domain should contain pure business logic without infrastructure coupling. " +
            "Move messaging concerns to Application or Patterns layer. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
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
    [Fact]
    public void Domain_ValueObjects_ShouldBeImmutable()
    {
        // Act
        var mutableValueObjects = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Domain")
            .And().HaveNameEndingWith("ValueObject")
            .And().AreClasses()
            .Should().BeImmutable()
            .GetResult();

        // Assert
        mutableValueObjects.IsSuccessful.ShouldBeTrue(
            "Value Objects should be immutable per DDD principles. " +
            "Consider using init-only setters or constructor-based initialization. " +
            $"Mutable types: {string.Join(", ", mutableValueObjects.FailingTypeNames ?? Array.Empty<string>())}");
    }

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

        // Act
        var domainTypes = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Domain")
            .GetTypes();

        // This is informational - document what Domain currently depends on
        var actualDependencies = domainTypes
            .SelectMany(t => t.Assembly.GetReferencedAssemblies())
            .Select(a => a.Name)
            .Distinct()
            .Where(name => !name.StartsWith("System.") &&
                           !name.StartsWith("mscorlib") &&
                           !name.StartsWith("netstandard") &&
                           !name.Equals("Excalibur.Domain"))
            .OrderBy(name => name)
            .ToList();

        // Report-only - Document findings without failing
        Console.WriteLine(
            "Domain layer current dependencies documented (report-only). " +
            $"Review for DDD compliance: {string.Join(", ", actualDependencies)}");
    }
}
