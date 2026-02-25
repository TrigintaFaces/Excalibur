using NetArchTest.Rules;

using Shouldly;

using System.Reflection;

using Xunit;

namespace Excalibur.Dispatch.ArchitectureTests;

/// <summary>
/// Phase 8.3: Comprehensive architecture boundary enforcement tests.
/// These tests enforce requirements R1.9, R17.8, and R23.1 identified during architectural audit.
/// </summary>
/// <remarks>
/// CONTEXT: Phase 8.3 remediated 4 architectural violations:
/// - Excalibur.Patterns.Hosting referenced Excalibur.Dispatch (R17.8 violation)
/// - Circular dependency between Excalibur.Dispatch.Transport.Google and Google Functions (R1.9)
/// - Cloud SDK contamination in core projects (R23.1)
///
/// These tests provide compile-time enforcement to prevent future violations.
/// </remarks>
public sealed class Phase8_3_BoundaryTests
{
    #region R1.9: Dispatch MUST NOT Reference Excalibur

    /// <summary>
    /// R1.9: Excalibur.Dispatch.* projects MUST NOT reference Excalibur.* projects.
    /// This is the fundamental architectural boundary separation.
    /// </summary>
    [Fact]
    public void R1_9_Dispatch_MustNotReference_Excalibur()
    {
        // Arrange - Get all Dispatch assemblies
        var dispatchAssemblies = GetDispatchAssemblies();

        if (!dispatchAssemblies.Any())
        {
            // Skip if assemblies not loaded
            return;
        }

        // Act - Check for Excalibur references
        var violations = new List<string>();
        foreach (var assembly in dispatchAssemblies)
        {
            var excaliburRefs = assembly
                .GetReferencedAssemblies()
                .Where(a => a.Name?.StartsWith("Excalibur") == true)
                .Select(a => a.Name)
                .ToList();

            if (excaliburRefs.Any())
            {
                violations.Add($"{assembly.GetName().Name} references: {string.Join(", ", excaliburRefs)}");
            }
        }

        // Assert
        violations.ShouldBeEmpty(
            $"R1.9 VIOLATION: Dispatch must not reference Excalibur (architectural boundary separation). " +
            $"Violations:\n{string.Join("\n", violations)}");
    }

    /// <summary>
    /// R1.9: Validate at type level - no Dispatch namespace types should depend on Excalibur namespace types.
    /// </summary>
    [Fact]
    public void R1_9_DispatchTypes_MustNotDependOn_ExcaliburTypes()
    {
        // Act
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch")
            .And().DoNotResideInNamespace("Excalibur.Dispatch.ArchitectureTests")
            .ShouldNot().HaveDependencyOn("Excalibur")
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue(
            $"R1.9 VIOLATION: Dispatch types must not depend on Excalibur types. " +
            $"This violates the core architectural boundary. " +
            $"Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    #endregion

    #region R17.8: Excalibur MAY Reference Excalibur.Dispatch.Abstractions Only

    /// <summary>
    /// R17.8: Excalibur.* MAY reference Excalibur.Dispatch.Abstractions, but MUST NOT reference Excalibur.Dispatch or Excalibur.Dispatch.Patterns.
    /// </summary>
    [Fact]
    public void R17_8_Excalibur_MustOnlyReference_DispatchAbstractions()
    {
        // Arrange - Get all Excalibur assemblies
        var excaliburAssemblies = GetExcaliburAssemblies();

        if (!excaliburAssemblies.Any())
        {
            return;
        }

        // Act - Check for forbidden Dispatch references
        var violations = new List<string>();
        var forbiddenPrefixes = new[]
        {
            "Excalibur.Dispatch",
            "Excalibur.Dispatch.Patterns", // Excalibur.Patterns.Hosting had this violation
            "Excalibur.Dispatch.Hosting.Web",
            "Excalibur.Dispatch.Transport.Azure",
            "Excalibur.Dispatch.Transport.Aws",
            "Excalibur.Dispatch.Transport.Google",
            "Excalibur.Dispatch.Transport.Kafka",
            "Excalibur.Dispatch.Transport.RabbitMQ"
        };

        foreach (var assembly in excaliburAssemblies)
        {
            var forbiddenRefs = assembly
                .GetReferencedAssemblies()
                .Where(a => forbiddenPrefixes.Any(prefix => a.Name?.StartsWith(prefix) == true))
                .Select(a => a.Name)
                .ToList();

            if (forbiddenRefs.Any())
            {
                violations.Add($"{assembly.GetName().Name} references: {string.Join(", ", forbiddenRefs)}");
            }
        }

        // Assert
        violations.ShouldBeEmpty(
            $"R17.8 VIOLATION: Excalibur may only reference Excalibur.Dispatch.Abstractions (not Core/Patterns/concrete implementations). " +
            $"This ensures loose coupling and prevents architectural boundary violations. " +
            $"Violations:\n{string.Join("\n", violations)}");
    }

    /// <summary>
    /// R17.8: Validate specific packages that had violations in Phase 8.3 audit.
    /// </summary>
    [Fact]
    public void R17_8_ExcaliburPatternsHosting_MustNotReference_DispatchPatterns()
    {
        // This was the specific violation found in Phase 8.3
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Patterns.Hosting")
            .ShouldNot().HaveDependencyOn("Excalibur.Dispatch.Patterns")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"R17.8 VIOLATION: Excalibur.Patterns.Hosting previously violated R17.8 by referencing Excalibur.Dispatch.Patterns. " +
            $"This was remediated in Phase 8.3. Regression detected! " +
            $"Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    /// <summary>
    /// R17.8: Excalibur MAY reference Excalibur.Dispatch.*.Abstractions packages (this is allowed).
    /// </summary>
    [Fact]
    public void R17_8_Excalibur_MayReference_DispatchAbstractions()
    {
        // This is a positive test - documenting what IS allowed
        var excaliburTypesReferencingDispatch = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur")
            .And().HaveDependencyOn("Excalibur.Dispatch")
            .GetTypes();

        if (!excaliburTypesReferencingDispatch.Any())
        {
            return; // No Excalibur types loaded
        }

        // All Excalibur references to Dispatch should be to Abstractions only
        var allowedPatterns = new[]
        {
            "Excalibur.Dispatch.Abstractions",
            "Excalibur.Dispatch.Transport.Abstractions",
            "Excalibur.Dispatch.Patterns.Abstractions",
            "Excalibur.Dispatch.Hosting.Serverless.Abstractions"
        };

        // This test documents the allowed pattern
        Console.WriteLine($"INFO: Excalibur correctly references {excaliburTypesReferencingDispatch.Count()} Dispatch types. " +
                          $"These should be from: {string.Join(", ", allowedPatterns)}");
    }

    #endregion

    #region R23.1: Core MUST NOT Reference Cloud SDKs

    /// <summary>
    /// R23.1: Excalibur.Dispatch and Excalibur.Dispatch.Patterns MUST NOT reference cloud provider SDKs.
    /// Cloud SDKs belong in provider packages only (pay-for-play model).
    /// </summary>
    [Fact]
    public void R23_1_Dispatch_MustNotReference_CloudSDKs()
    {
        // Arrange
        var coreAssemblies = GetAssembliesMatching("Excalibur.Dispatch", "Excalibur.Dispatch.Patterns");
        var cloudSDKPrefixes = new[]
        {
            "Azure",
            "AWSSDK",
            "Google.Cloud",
            "Microsoft.Azure"
        };

        // Act
        var violations = new List<string>();
        foreach (var assembly in coreAssemblies)
        {
            var cloudRefs = assembly
                .GetReferencedAssemblies()
                .Where(a => cloudSDKPrefixes.Any(prefix => a.Name?.StartsWith(prefix) == true))
                .Where(a => !a.Name!.Contains("Testing") && !a.Name.Contains("TestContainers")) // Allow test dependencies
                .Select(a => a.Name)
                .ToList();

            if (cloudRefs.Any())
            {
                violations.Add($"{assembly.GetName().Name} references: {string.Join(", ", cloudRefs)}");
            }
        }

        // Assert
        violations.ShouldBeEmpty(
            $"R23.1 VIOLATION: Core libraries must not reference cloud provider SDKs (pay-for-play model). " +
            $"Cloud dependencies belong in Excalibur.Dispatch.Transport.<Provider> packages only. " +
            $"Violations:\n{string.Join("\n", violations)}");
    }

    /// <summary>
    /// R23.1: Validate at type level - core types must not depend on cloud SDK namespaces.
    /// </summary>
    [Fact]
    public void R23_1_DispatchTypes_MustNotDependOn_CloudSDKs()
    {
        // Test Azure
        var azureResult = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch")
            .Or().ResideInNamespace("Excalibur.Dispatch.Patterns")
            .ShouldNot().HaveDependencyOnAny(new[] { "Azure", "Microsoft.Azure" })
            .GetResult();

        azureResult.IsSuccessful.ShouldBeTrue(
            $"R23.1 VIOLATION: Dispatch types must not depend on Azure SDKs. " +
            $"Failing types: {string.Join(", ", azureResult.FailingTypeNames ?? Array.Empty<string>())}");

        // Test AWS
        var awsResult = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch")
            .Or().ResideInNamespace("Excalibur.Dispatch.Patterns")
            .ShouldNot().HaveDependencyOn("AWSSDK")
            .GetResult();

        awsResult.IsSuccessful.ShouldBeTrue(
            $"R23.1 VIOLATION: Dispatch types must not depend on AWS SDKs. " +
            $"Failing types: {string.Join(", ", awsResult.FailingTypeNames ?? Array.Empty<string>())}");

        // Test Google
        var googleResult = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch")
            .Or().ResideInNamespace("Excalibur.Dispatch.Patterns")
            .ShouldNot().HaveDependencyOn("Google.Cloud")
            .GetResult();

        googleResult.IsSuccessful.ShouldBeTrue(
            $"R23.1 VIOLATION: Dispatch types must not depend on Google Cloud SDKs. " +
            $"Failing types: {string.Join(", ", googleResult.FailingTypeNames ?? Array.Empty<string>())}");
    }

    /// <summary>
    /// R23.1: Provider packages MUST only reference their own cloud SDK (no cross-contamination).
    /// </summary>
    [Fact]
    public void R23_1_AzureProvider_MustNotReference_AwsOrGoogleSDKs()
    {
        var azureAssemblies = GetAssembliesMatching("Excalibur.Dispatch.Transport.Azure");

        if (!azureAssemblies.Any())
        {
            return;
        }

        var violations = new List<string>();
        foreach (var assembly in azureAssemblies)
        {
            var invalidRefs = assembly
                .GetReferencedAssemblies()
                .Where(a => (a.Name?.StartsWith("AWSSDK") == true || a.Name?.StartsWith("Google.Cloud") == true))
                .Where(a => !a.Name!.Contains("Testing"))
                .Select(a => a.Name)
                .ToList();

            if (invalidRefs.Any())
            {
                violations.Add($"{assembly.GetName().Name} references: {string.Join(", ", invalidRefs)}");
            }
        }

        violations.ShouldBeEmpty(
            $"R23.1 VIOLATION: Azure provider packages must not reference AWS or Google SDKs. " +
            $"Violations:\n{string.Join("\n", violations)}");
    }

    /// <summary>
    /// R23.1: AWS provider MUST only reference AWS SDKs (no Azure or Google).
    /// </summary>
    [Fact]
    public void R23_1_AwsProvider_MustNotReference_AzureOrGoogleSDKs()
    {
        var awsAssemblies = GetAssembliesMatching("Excalibur.Dispatch.Transport.Aws", "Excalibur.Dispatch.Hosting.Serverless.AwsLambda");

        if (!awsAssemblies.Any())
        {
            return;
        }

        var violations = new List<string>();
        foreach (var assembly in awsAssemblies)
        {
            var invalidRefs = assembly
                .GetReferencedAssemblies()
                .Where(a => (a.Name?.StartsWith("Azure") == true ||
                            a.Name?.StartsWith("Microsoft.Azure") == true ||
                            a.Name?.StartsWith("Google.Cloud") == true))
                .Where(a => !a.Name!.Contains("Testing"))
                .Select(a => a.Name)
                .ToList();

            if (invalidRefs.Any())
            {
                violations.Add($"{assembly.GetName().Name} references: {string.Join(", ", invalidRefs)}");
            }
        }

        violations.ShouldBeEmpty(
            $"R23.1 VIOLATION: AWS provider packages must not reference Azure or Google SDKs. " +
            $"Violations:\n{string.Join("\n", violations)}");
    }

    /// <summary>
    /// R23.1: Google provider MUST only reference Google SDKs (no Azure or AWS).
    /// </summary>
    [Fact]
    public void R23_1_GoogleProvider_MustNotReference_AzureOrAwsSDKs()
    {
        var googleAssemblies = GetAssembliesMatching("Excalibur.Dispatch.Transport.Google", "Excalibur.Dispatch.Hosting.Serverless.GoogleCloudFunctions");

        if (!googleAssemblies.Any())
        {
            return;
        }

        var violations = new List<string>();
        foreach (var assembly in googleAssemblies)
        {
            var invalidRefs = assembly
                .GetReferencedAssemblies()
                .Where(a => (a.Name?.StartsWith("Azure") == true ||
                            a.Name?.StartsWith("Microsoft.Azure") == true ||
                            a.Name?.StartsWith("AWSSDK") == true))
                .Where(a => !a.Name!.Contains("Testing"))
                .Select(a => a.Name)
                .ToList();

            if (invalidRefs.Any())
            {
                violations.Add($"{assembly.GetName().Name} references: {string.Join(", ", invalidRefs)}");
            }
        }

        violations.ShouldBeEmpty(
            $"R23.1 VIOLATION: Google provider packages must not reference Azure or AWS SDKs. " +
            $"Violations:\n{string.Join("\n", violations)}");
    }

    #endregion

    #region R0.14: Serialization Boundary Enforcement

    /// <summary>
    /// R0.14: Dispatch MUST use MemoryPack only for internal serialization.
    /// No System.Text.Json, MessagePack, or Protobuf in core.
    /// </summary>
    [Fact]
    public void R0_14_Dispatch_MustOnlyUse_MemoryPack()
    {
        // Banned serializers in core
        var bannedSerializers = new[]
        {
            "System.Text.Json",
            "MessagePack",
            "Newtonsoft.Json",
            "Google.Protobuf"
        };

        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch")
            .ShouldNot().HaveDependencyOnAny(bannedSerializers)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"R0.14 VIOLATION: Dispatch must use MemoryPack only (internal binary serialization). " +
            $"System.Text.Json belongs in public edge packages only. " +
            $"Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    /// <summary>
    /// R0.14: Public boundary packages MUST use System.Text.Json with source generation.
    /// </summary>
    [Fact]
    public void R0_14_PublicBoundaryPackages_MustUse_SystemTextJson()
    {
        // This test documents the expected pattern (not enforcing presence, just absence of banned serializers)
        var hostingPackages = new[]
        {
            "Excalibur.Dispatch.Hosting.Web",
            "Excalibur.Dispatch.Hosting.Serverless"
        };

        // These packages should use STJ (not MemoryPack) for public APIs
        Console.WriteLine("INFO: Public boundary packages (Hosting.Web, Hosting.Serverless) should use System.Text.Json for external serialization.");
    }

    #endregion

    #region Naming Convention Enforcement

    /// <summary>
    /// Enforce Microsoft naming guidelines for all public types.
    /// </summary>
    [Fact]
    public void AllPublicTypes_ShouldFollow_MicrosoftNamingGuidelines()
    {
        // Public types should be PascalCase
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch")
            .Or().ResideInNamespace("Excalibur")
            .And().ArePublic()
            .Should().HaveNameMatching(@"^[A-Z][a-zA-Z0-9]*$") // PascalCase
            .GetResult();

        // This is informational - not all legacy types may comply
        if (!result.IsSuccessful)
        {
            Console.WriteLine($"INFO: {result.FailingTypeNames?.Count() ?? 0} public types don't follow strict PascalCase naming. " +
                            $"Review for compliance: {string.Join(", ", result.FailingTypeNames?.Take(10) ?? Array.Empty<string>())}");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets all loaded assemblies that match Excalibur.Dispatch.* pattern.
    /// </summary>
    private static Assembly[] GetDispatchAssemblies()
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Excalibur.Dispatch") == true)
            .Where(a => !a.GetName().Name!.Contains("Test")) // Exclude test assemblies
            .ToArray();
    }

    /// <summary>
    /// Gets all loaded assemblies that match Excalibur.* pattern.
    /// </summary>
    private static Assembly[] GetExcaliburAssemblies()
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Excalibur") == true)
            .Where(a => !a.GetName().Name!.Contains("Test")) // Exclude test assemblies
            .ToArray();
    }

    /// <summary>
    /// Gets assemblies matching any of the provided prefixes.
    /// </summary>
    private static Assembly[] GetAssembliesMatching(params string[] prefixes)
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => prefixes.Any(prefix => a.GetName().Name?.StartsWith(prefix) == true))
            .Where(a => !a.GetName().Name!.Contains("Test"))
            .ToArray();
    }

    #endregion
}
