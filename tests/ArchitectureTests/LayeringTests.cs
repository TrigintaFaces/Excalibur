using NetArchTest.Rules;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.ArchitectureTests;

/// <summary>
/// Validates architectural layering boundaries per management/package-map.yaml.
/// These tests enforce the 6-tier canonical structure to prevent coupling violations.
/// </summary>
public class LayeringTests
{
    /// <summary>
    /// TIER 1 RULE: Abstractions packages must not reference any implementation packages.
    /// Allowed dependencies: BCL + Microsoft.Extensions.*.Abstractions only.
    ///
    /// VIOLATIONS DETECTED IN AUDIT:
    /// - Excalibur.Dispatch.Patterns.Abstractions references Excalibur.Dispatch (CRITICAL)
    /// - Excalibur.Dispatch.Transport.Abstractions references CloudNative.CloudEvents (acceptable - shared contract)
    /// </summary>
    [Fact]
    public void Tier1_Abstractions_ShouldNotReference_ImplementationPackages()
    {
        // Arrange
        var abstractionNamespaces = new[]
        {
            "Excalibur.Dispatch.Abstractions",
            "Excalibur.Dispatch.Patterns.Abstractions",
            "Excalibur.Dispatch.Transport.Abstractions",
            "Excalibur.Dispatch.Hosting.Serverless.Abstractions",
            "Excalibur.Data.Abstractions",
            "Excalibur.Jobs.Abstractions"
        };

        var prohibitedImplementations = new[]
        {
            "Excalibur.Dispatch",
            "Excalibur.Dispatch.Patterns",
            "Excalibur.Dispatch.Transport.Azure",
            "Excalibur.Dispatch.Transport.Aws",
            "Excalibur.Dispatch.Transport.Google",
            "Excalibur.Dispatch.Transport.Kafka",
            "Excalibur.Dispatch.Transport.RabbitMQ",
            "Excalibur.Application",
            "Excalibur.Data",
            "Excalibur.Jobs"
        };

        // Act
        foreach (var abstractionNamespace in abstractionNamespaces)
        {
            var result = Types.InCurrentDomain()
                .That().ResideInNamespace(abstractionNamespace)
                .ShouldNot().HaveDependencyOnAny(prohibitedImplementations)
                .GetResult();

            // Assert
            result.IsSuccessful.ShouldBeTrue(
                $"Abstraction package '{abstractionNamespace}' must not reference implementation packages. " +
                $"Violations found: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
        }
    }

    /// <summary>
    /// TIER 2 RULE: Core implementation must not reference provider SDKs.
    /// Dispatch must remain cloud-agnostic with zero vendor coupling.
    ///
    /// CRITICAL VIOLATIONS DETECTED:
    /// - Dispatch references Microsoft.Data.SqlClient (PROVIDER_LEAKAGE)
    /// - Dispatch references Npgsql (PROVIDER_LEAKAGE)
    /// - Dispatch references Dapper (acceptable - data access abstraction)
    /// </summary>
    [Fact]
    public void Tier2_Dispatch_ShouldNotReference_ProviderSDKs()
    {
        // Arrange
        var prohibitedProviders = new[]
        {
            "Microsoft.Data.SqlClient",
            "Npgsql",
            "Azure.Messaging",
            "Azure.Storage",
            "Azure.Identity",
            "AWSSDK",
            "Amazon.Lambda",
            "Google.Cloud",
            "Google.Apis",
            "Confluent.Kafka",
            "RabbitMQ.Client",
            "MongoDB.Driver",
            "StackExchange.Redis",
            "Elastic.Clients"
        };

        // Act
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch")
            .ShouldNot().HaveDependencyOnAny(prohibitedProviders)
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue(
            "Dispatch must be provider-agnostic and not reference any cloud or data provider SDKs. " +
            "These dependencies must live in provider packages (Excalibur.Dispatch.Transport.*, Excalibur.Data.*). " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    /// <summary>
    /// TIER 3 RULE: Hosting integrations should reference both abstractions and core implementation.
    /// This is the CORRECT pattern for integration packages.
    ///
    /// CLEAN PATTERN CONFIRMED:
    /// - Excalibur.Dispatch.Hosting.Web ✅
    /// - Excalibur.Dispatch.Hosting.Serverless.* packages ✅
    /// </summary>
    [Fact]
    public void Tier3_HostingPackages_MayReference_Implementation()
    {
        // Arrange
        var hostingNamespaces = new[]
        {
            "Excalibur.Dispatch.Hosting.Web",
            "Excalibur.Dispatch.Hosting.Serverless.AwsLambda",
            "Excalibur.Dispatch.Hosting.Serverless.AzureFunctions",
            "Excalibur.Dispatch.Hosting.Serverless.GoogleCloudFunctions",
            "Excalibur.Hosting",
            "Excalibur.Hosting.Jobs",
            "Excalibur.Hosting.Web"
        };

        // Act & Assert
        foreach (var hostingNamespace in hostingNamespaces)
        {
            var typesExist = Types.InCurrentDomain()
                .That().ResideInNamespace(hostingNamespace)
                .GetTypes()
                .Any();

            if (typesExist)
            {
                // Hosting packages SHOULD reference Dispatch - this is allowed
                var hasDispatchReference = Types.InCurrentDomain()
                    .That().ResideInNamespace(hostingNamespace)
                    .Should().HaveDependencyOn("Excalibur.Dispatch")
                    .GetResult()
                    .IsSuccessful;

                // This assertion documents the pattern - hosting integrations bridge abstractions and implementation
                hasDispatchReference.ShouldBeTrue(
                    $"Hosting package '{hostingNamespace}' should reference Dispatch to wire up implementation. " +
                    "This is the correct pattern for Tier 3 integration packages.");
            }
        }
    }

    /// <summary>
    /// TIER 5 RULE: Provider packages must ONLY reference their specific provider SDKs.
    /// No cross-provider contamination allowed (Azure package cannot reference AWS SDK).
    ///
    /// CLEAN PATTERNS CONFIRMED:
    /// - Excalibur.Dispatch.Transport.Azure only references Azure.* ✅
    /// - Excalibur.Dispatch.Transport.Aws only references AWSSDK.* ✅
    /// - Excalibur.Dispatch.Transport.Google only references Google.* ✅
    /// </summary>
    [Fact]
    public void Tier5_AzureProvider_ShouldNotReference_AwsOrGoogleSDKs()
    {
        // Act
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch.Transport.Azure")
            .ShouldNot().HaveDependencyOnAny(new[]
            {
                "AWSSDK",
                "Amazon.Lambda",
                "Google.Cloud",
                "Google.Apis"
            })
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue(
            "Azure provider must not reference AWS or Google SDKs. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Tier5_AwsProvider_ShouldNotReference_AzureOrGoogleSDKs()
    {
        // Act
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch.Transport.Aws")
            .ShouldNot().HaveDependencyOnAny(new[]
            {
                "Azure.Messaging",
                "Azure.Storage",
                "Google.Cloud",
                "Google.Apis"
            })
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue(
            "AWS provider must not reference Azure or Google SDKs. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void Tier5_GoogleProvider_ShouldNotReference_AzureOrAwsSDKs()
    {
        // Act
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch.Transport.Google")
            .ShouldNot().HaveDependencyOnAny(new[]
            {
                "Azure.Messaging",
                "Azure.Storage",
                "AWSSDK",
                "Amazon.Lambda"
            })
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue(
            "Google provider must not reference Azure or AWS SDKs. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    /// <summary>
    /// TIER 6 RULE: Excalibur.* packages should minimize coupling to Excalibur.Dispatch.
    /// Prefer referencing Excalibur.Dispatch.Abstractions for loose coupling.
    ///
    /// PERVASIVE VIOLATIONS DETECTED:
    /// - 15 Excalibur.* packages reference Dispatch directly
    /// - This creates tight coupling and prevents proper abstraction
    /// </summary>
    [Fact]
    public void Tier6_ExcaliburPackages_ShouldPrefer_DispatchAbstractions_Over_Dispatch()
    {
        // Arrange
        var excaliburPackages = new[]
        {
            "Excalibur.Application",
            "Excalibur.Data",
            "Excalibur.Patterns",
            "Excalibur.Jobs"
        };

        // Act & Assert
        foreach (var excaliburPackage in excaliburPackages)
        {
            var hasAbstractionsReference = Types.InCurrentDomain()
                .That().ResideInNamespace(excaliburPackage)
                .Should().HaveDependencyOn("Excalibur.Dispatch.Abstractions")
                .GetResult()
                .IsSuccessful;

            var hasDispatchReference = Types.InCurrentDomain()
                .That().ResideInNamespace(excaliburPackage)
                .Should().HaveDependencyOn("Excalibur.Dispatch")
                .GetResult()
                .IsSuccessful;

            // Document current state: most Excalibur packages ARE coupled to Dispatch
            // This test will FAIL until refactoring is complete
            if (hasDispatchReference && !hasAbstractionsReference)
            {
                Assert.Fail(
                    $"Package '{excaliburPackage}' references Dispatch but not Excalibur.Dispatch.Abstractions. " +
                    "This creates tight coupling. Prefer abstractions for loose coupling. " +
                    "See management/package-map.yaml for remediation plan.");
            }
        }
    }
}
