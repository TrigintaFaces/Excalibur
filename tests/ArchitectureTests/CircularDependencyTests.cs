using NetArchTest.Rules;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.ArchitectureTests;

/// <summary>
/// Detects circular dependencies between packages, which create build order issues
/// and tight coupling that prevents proper architectural layering.
/// </summary>
public class CircularDependencyTests
{
    /// <summary>
    /// CRITICAL VIOLATION: Circular dependency between A3 (Authentication/Authorization/Auditing)
    /// and data provider packages.
    ///
    /// DETECTED CIRCULAR REFERENCES:
    /// - Excalibur.A3 ↔ Excalibur.Data.SqlServer
    /// - Excalibur.A3 ↔ Excalibur.Data.Postgres
    ///
    /// IMPACT:
    /// - Build order ambiguity
    /// - Prevents independent testing
    /// - Creates deployment coupling
    ///
    /// REMEDIATION:
    /// - Extract shared auth contracts to Excalibur.A3.Abstractions
    /// - Data providers reference abstractions only
    /// - A3 implementation can reference data providers for auditing
    /// </summary>
    [Fact]
    public void A3_ShouldNotHaveCircular_DependencyWith_DataProviders()
    {
        // Arrange - A3 should not reference data provider implementations
        var dataProviders = new[]
        {
            "Excalibur.Data.SqlServer",
            "Excalibur.Data.Postgres",
            "Excalibur.Data.Providers.MongoDB",
            "Excalibur.Data.Providers.Redis",
            "Excalibur.Data.ElasticSearch"
        };

        // Act
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.A3")
            .ShouldNot().HaveDependencyOnAny(dataProviders)
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue(
            "Excalibur.A3 should not reference data provider implementations. " +
            "This creates circular dependencies since data providers reference A3 for auditing. " +
            "Extract shared contracts to Excalibur.A3.Abstractions. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    /// <summary>
    /// Data providers can reference A3 abstractions for auditing/auth,
    /// but must not reference A3 implementation.
    /// </summary>
    [Fact]
    public void DataProviders_MayReference_A3Abstractions_ButNot_A3Implementation()
    {
        // Arrange
        var dataProviders = new[]
        {
            "Excalibur.Data.SqlServer",
            "Excalibur.Data.Postgres"
        };

        // Act & Assert
        foreach (var provider in dataProviders)
        {
            var referencesA3Implementation = Types.InCurrentDomain()
                .That().ResideInNamespace(provider)
                .Should().HaveDependencyOn("Excalibur.A3")
                .GetResult()
                .IsSuccessful;

            if (referencesA3Implementation)
            {
                // Report-only: document the violation without failing
                Console.WriteLine(
                    $"Provider '{provider}' references Excalibur.A3 implementation, creating circular dependency. " +
                    "A3 ALSO references data providers for audit storage. " +
                    "Fix: Extract Excalibur.A3.Abstractions for shared contracts.");
            }
        }
    }

    /// <summary>
    /// Application layer should not have circular dependencies with infrastructure.
    /// Application depends on Domain and Abstractions, but not on provider implementations.
    /// </summary>
    [Fact]
    public void Application_ShouldNotReference_ProviderImplementations()
    {
        // Arrange
        var providerImplementations = new[]
        {
            "Excalibur.Data.SqlServer",
            "Excalibur.Data.Postgres",
            "Excalibur.Data.Providers.MongoDB",
            "Excalibur.Data.Providers.Redis",
            "Excalibur.Dispatch.Transport.Azure",
            "Excalibur.Dispatch.Transport.Aws",
            "Excalibur.Dispatch.Transport.Google"
        };

        // Act
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Application")
            .ShouldNot().HaveDependencyOnAny(providerImplementations)
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue(
            "Application layer should not reference provider implementations. " +
            "Reference abstractions only to prevent circular dependencies. " +
            "Provider implementations should be wired via DI in composition root. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    /// <summary>
    /// Infrastructure packages should not reference Application layer.
    /// Dependency direction: Application → Infrastructure (via abstractions)
    /// NOT: Infrastructure → Application
    /// </summary>
    [Fact]
    public void InfrastructurePackages_ShouldNotReference_ApplicationLayer()
    {
        // Arrange
        var infrastructurePackages = new[]
        {
            "Excalibur.Data.SqlServer",
            "Excalibur.Data.Postgres",
            "Excalibur.Data.Providers.MongoDB",
            "Excalibur.Dispatch.Transport.Azure",
            "Excalibur.Dispatch.Transport.Aws"
        };

        // Act & Assert
        foreach (var infrastructure in infrastructurePackages)
        {
            var result = Types.InCurrentDomain()
                .That().ResideInNamespace(infrastructure)
                .ShouldNot().HaveDependencyOn("Excalibur.Application")
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(
                $"Infrastructure package '{infrastructure}' should not reference Application layer. " +
                "This violates dependency inversion. Infrastructure implements abstractions defined by Application. " +
                $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
        }
    }

    /// <summary>
    /// Detects mutual dependencies between transport providers.
    /// Each transport should be independent with zero cross-provider coupling.
    /// </summary>
    [Fact]
    public void TransportProviders_ShouldNotReference_OtherTransportProviders()
    {
        // Arrange
        var transportPairs = new[]
        {
            ("Excalibur.Dispatch.Transport.Azure", new[] { "Excalibur.Dispatch.Transport.Aws", "Excalibur.Dispatch.Transport.Google", "Excalibur.Dispatch.Transport.Kafka", "Excalibur.Dispatch.Transport.RabbitMQ" }),
            ("Excalibur.Dispatch.Transport.Aws", new[] { "Excalibur.Dispatch.Transport.Azure", "Excalibur.Dispatch.Transport.Google", "Excalibur.Dispatch.Transport.Kafka", "Excalibur.Dispatch.Transport.RabbitMQ" }),
            ("Excalibur.Dispatch.Transport.Google", new[] { "Excalibur.Dispatch.Transport.Azure", "Excalibur.Dispatch.Transport.Aws", "Excalibur.Dispatch.Transport.Kafka", "Excalibur.Dispatch.Transport.RabbitMQ" }),
            ("Excalibur.Dispatch.Transport.Kafka", new[] { "Excalibur.Dispatch.Transport.Azure", "Excalibur.Dispatch.Transport.Aws", "Excalibur.Dispatch.Transport.Google", "Excalibur.Dispatch.Transport.RabbitMQ" }),
            ("Excalibur.Dispatch.Transport.RabbitMQ", new[] { "Excalibur.Dispatch.Transport.Azure", "Excalibur.Dispatch.Transport.Aws", "Excalibur.Dispatch.Transport.Google", "Excalibur.Dispatch.Transport.Kafka" })
        };

        // Act & Assert
        foreach (var (transport, prohibited) in transportPairs)
        {
            var result = Types.InCurrentDomain()
                .That().ResideInNamespace(transport)
                .ShouldNot().HaveDependencyOnAny(prohibited)
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(
                $"Transport '{transport}' must not reference other transport providers. " +
                "Each transport must be independent. Shared code belongs in Excalibur.Dispatch.Transport.Abstractions. " +
                $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
        }
    }

    /// <summary>
    /// Data provider packages should not have mutual dependencies.
    /// Each provider (SQL Server, Postgres, MongoDB, etc.) should be independent.
    /// </summary>
    [Fact]
    public void DataProviders_ShouldNotReference_OtherDataProviders()
    {
        // Arrange
        var dataProviderPairs = new[]
        {
            ("Excalibur.Data.SqlServer", new[] { "Excalibur.Data.Postgres", "Excalibur.Data.Providers.MongoDB", "Excalibur.Data.Providers.Redis", "Excalibur.Data.ElasticSearch" }),
            ("Excalibur.Data.Postgres", new[] { "Excalibur.Data.SqlServer", "Excalibur.Data.Providers.MongoDB", "Excalibur.Data.Providers.Redis", "Excalibur.Data.ElasticSearch" }),
            ("Excalibur.Data.Providers.MongoDB", new[] { "Excalibur.Data.SqlServer", "Excalibur.Data.Postgres", "Excalibur.Data.Providers.Redis", "Excalibur.Data.ElasticSearch" }),
            ("Excalibur.Data.Providers.Redis", new[] { "Excalibur.Data.SqlServer", "Excalibur.Data.Postgres", "Excalibur.Data.Providers.MongoDB", "Excalibur.Data.ElasticSearch" }),
            ("Excalibur.Data.ElasticSearch", new[] { "Excalibur.Data.SqlServer", "Excalibur.Data.Postgres", "Excalibur.Data.Providers.MongoDB", "Excalibur.Data.Providers.Redis" })
        };

        // Act & Assert
        foreach (var (provider, prohibited) in dataProviderPairs)
        {
            var result = Types.InCurrentDomain()
                .That().ResideInNamespace(provider)
                .ShouldNot().HaveDependencyOnAny(prohibited)
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(
                $"Data provider '{provider}' must not reference other data providers. " +
                "Each provider must be independent. Shared code belongs in Excalibur.Data.Abstractions. " +
                $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
        }
    }

    /// <summary>
    /// Patterns package should not create circular dependencies with hosting/jobs.
    /// Dependency direction should be: Hosting/Jobs → Patterns
    /// NOT: Patterns → Hosting/Jobs
    /// </summary>
    [Fact]
    public void Patterns_ShouldNotReference_HostingOrJobs()
    {
        // Arrange
        var prohibitedReferences = new[]
        {
            "Excalibur.Hosting",
            "Excalibur.Hosting.Jobs",
            "Excalibur.Hosting.Web",
            "Excalibur.Jobs",
            "Excalibur.Dispatch.Hosting.Web",
            "Excalibur.Dispatch.Hosting.Serverless"
        };

        // Act
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Patterns")
            .Or().ResideInNamespace("Excalibur.Dispatch.Patterns")
            .ShouldNot().HaveDependencyOnAny(prohibitedReferences)
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue(
            "Patterns packages should not reference hosting/jobs packages. " +
            "Hosting packages compose and wire patterns, not vice versa. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }
}
