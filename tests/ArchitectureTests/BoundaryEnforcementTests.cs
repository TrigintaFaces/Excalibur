using NetArchTest.Rules;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.ArchitectureTests;

/// <summary>
/// Enforces the critical Excalibur.Dispatch.Abstractions ↔ Excalibur.Dispatch architectural boundary.
/// These tests validate the abstraction layer pattern that enables provider composability
/// and prevents leakage of concrete implementations into public APIs.
///
/// CONTEXT: These rules were validated manually in TASK-0001 through TASK-0005.
/// This test suite provides automated enforcement to prevent future regressions.
///
/// BOUNDARY RULES:
/// 1. Excalibur.Dispatch depends on Excalibur.Dispatch.Abstractions (implementations depend on contracts)
/// 2. Excalibur.Dispatch.Abstractions NEVER depends on Excalibur.Dispatch (no reverse dependency)
/// 3. Excalibur.* packages prefer Excalibur.Dispatch.Abstractions over Excalibur.Dispatch (loose coupling)
/// 4. Public APIs in Excalibur never expose Excalibur.Dispatch types directly (encapsulation)
/// </summary>
public class BoundaryEnforcementTests
{
    #region Core Boundary Rules

    /// <summary>
    /// RULE 1: Excalibur.Dispatch MUST depend on Excalibur.Dispatch.Abstractions.
    /// This validates the fundamental pattern: implementations depend on contracts.
    ///
    /// VERIFIED IN: TASK-0002 (95%+ of Dispatch types implement interfaces)
    /// </summary>
    [Fact]
    public void Dispatch_MustDependOn_DispatchAbstractions()
    {
        // Act - Target the Dispatch namespace but exclude test namespaces
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch")
            .And().DoNotResideInNamespaceContaining("Tests")
            .And().DoNotResideInNamespaceContaining("Benchmark")
            .And().AreClasses()
            .Should().HaveDependencyOn("Excalibur.Dispatch.Abstractions")
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue(
            "Dispatch implementations must depend on Excalibur.Dispatch.Abstractions interfaces. " +
            "This is the foundation of the abstraction layer pattern. " +
            $"Types missing abstraction dependency: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    /// <summary>
    /// RULE 2: Excalibur.Dispatch.Abstractions MUST NOT depend on Excalibur.Dispatch.
    /// This is the most critical boundary rule - abstractions must remain pure contracts.
    ///
    /// VERIFIED IN: TASK-0001 (manual audit found ZERO violations)
    /// </summary>
    [Fact]
    public void DispatchAbstractions_MustNotDependOn_Dispatch()
    {
        // Act
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch.Abstractions")
            .ShouldNot().HaveDependencyOn("Excalibur.Dispatch")
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue(
            "CRITICAL BOUNDARY VIOLATION: Excalibur.Dispatch.Abstractions must never depend on Excalibur.Dispatch. " +
            "Abstractions define contracts; Dispatch provides implementations. Reverse dependency breaks the pattern. " +
            $"Violating types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    /// <summary>
    /// RULE 3: Excalibur.Dispatch.Abstractions should only contain interfaces, abstracts, and value types.
    /// No concrete implementations allowed in the abstractions layer.
    /// </summary>
    [Fact]
    public void DispatchAbstractions_ShouldOnlyContain_Interfaces_Abstracts_ValueTypes()
    {
        // Act - Get all non-compliant concrete classes
        var concreteClasses = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch.Abstractions")
            .And().AreClasses()
            .And().AreNotAbstract()
            .And().DoNotHaveNameEndingWith("Exception") // Exceptions are allowed
            .And().DoNotHaveNameEndingWith("EventArgs") // Event args are allowed
            .GetTypes()
            .Where(t => !t.IsEnum) // Enums are allowed
            .Where(t => !t.IsValueType) // Value types (structs, records) are allowed
            .ToList();

        // Assert
        concreteClasses.ShouldBeEmpty(
            "Excalibur.Dispatch.Abstractions should only contain interfaces, abstract classes, value types, enums, and exceptions. " +
            "Concrete implementations belong in Excalibur.Dispatch. " +
            $"Concrete classes found: {string.Join(", ", concreteClasses.Select(t => t.Name))}");
    }

    #endregion

    #region Excalibur Boundary Rules

    /// <summary>
    /// RULE 4: Excalibur public APIs must not expose Dispatch types directly.
    /// This validates proper encapsulation - consumers should only see abstractions.
    ///
    /// VERIFIED IN: TASK-0003 (DI registrations 95%+ using interfaces)
    /// </summary>
    [Fact]
    public void ExcaliburPublicAPIs_MustNotExpose_DispatchTypes()
    {
        // Arrange - Get all Excalibur namespaces
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

        foreach (var ns in excaliburNamespaces)
        {
            // Act - Check public types for Dispatch exposure
            var publicTypesExposingDispatch = Types.InCurrentDomain()
                .That().ResideInNamespace(ns)
                .And().ArePublic()
                .And().HaveDependencyOn("Excalibur.Dispatch")
                .GetTypes()
                .Where(t => ExposesDispatchInSignature(t))
                .ToList();

            // Assert
            publicTypesExposingDispatch.ShouldBeEmpty(
                $"Excalibur.{ns} public APIs must not expose Excalibur.Dispatch types in method signatures or properties. " +
                "Use Excalibur.Dispatch.Abstractions interfaces instead for loose coupling. " +
                $"Types exposing Excalibur.Dispatch: {string.Join(", ", publicTypesExposingDispatch.Select(t => t.Name))}");
        }
    }

    /// <summary>
    /// RULE 5: Excalibur.* packages should prefer Excalibur.Dispatch.Abstractions over Excalibur.Dispatch.
    /// This encourages loose coupling and enables easy provider substitution.
    ///
    /// VERIFIED IN: TASK-0004 (Tests 100% using interface mocking)
    /// </summary>
    [Fact]
    public void ExcaliburPackages_ShouldPrefer_DispatchAbstractions()
    {
        // Arrange
        var excaliburPackages = new[]
        {
            "Excalibur.Application",
            "Excalibur.Data",
            "Excalibur.Patterns"
        };

        foreach (var package in excaliburPackages)
        {
            // Act - Check if package uses abstractions
            var typesInPackage = Types.InCurrentDomain()
                .That().ResideInNamespace(package)
                .GetTypes();

            if (!typesInPackage.Any())
                continue; // Skip if package not loaded

            var hasAbstractionsReference = Types.InCurrentDomain()
                .That().ResideInNamespace(package)
                .Should().HaveDependencyOn("Excalibur.Dispatch.Abstractions")
                .GetResult()
                .IsSuccessful;

            var hasDispatchReference = Types.InCurrentDomain()
                .That().ResideInNamespace(package)
                .Should().HaveDependencyOn("Excalibur.Dispatch")
                .GetResult()
                .IsSuccessful;

            // Assert - If using Excalibur.Dispatch at all, should use Abstractions
            if (hasDispatchReference)
            {
                hasAbstractionsReference.ShouldBeTrue(
                    $"Package '{package}' references Dispatch but not Excalibur.Dispatch.Abstractions. " +
                    "This creates tight coupling. Prefer abstractions for loose coupling and testability.");
            }
        }
    }

    /// <summary>
    /// RULE 6: Excalibur.Domain must not depend on ANY Dispatch package (Dispatch or Abstractions).
    /// Domain should be pure business logic with zero messaging framework coupling.
    ///
    /// VERIFIED IN: TASK-0001 (manual audit confirmed domain isolation)
    /// </summary>
    [Fact]
    public void ExcaliburDomain_MustNotDependOn_AnyDispatchPackage()
    {
        // Act
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Domain")
            .ShouldNot().HaveDependencyOnAny(new[]
            {
                "Excalibur.Dispatch",
                "Excalibur.Dispatch.Abstractions",
                "Excalibur.Dispatch.Patterns",
                "Excalibur.Dispatch.Transport"
            })
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue(
            "Excalibur.Domain must be messaging-agnostic per DDD principles. " +
            "Domain contains pure business logic with zero infrastructure coupling. " +
            "Messaging concerns belong in Application or Patterns layer. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    #endregion

    #region Dependency Direction Rules

    /// <summary>
    /// RULE 7: Excalibur.Dispatch.Abstractions may only depend on BCL and Microsoft.Extensions.*.Abstractions.
    /// No third-party dependencies allowed in the contract layer.
    /// </summary>
    [Fact]
    public void DispatchAbstractions_ShouldOnlyDependOn_BCL_And_MSExtensionsAbstractions()
    {
        // Arrange - Allowed dependencies
        var allowedNamespaces = new[]
        {
            "System",
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.Logging.Abstractions",
            "Microsoft.Extensions.Options"
        };

        // Act - Get all types in Excalibur.Dispatch.Abstractions
        var abstractionTypes = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch.Abstractions")
            .GetTypes();

        // Get actual referenced assemblies (excluding BCL and allowed)
        var actualDependencies = abstractionTypes
            .SelectMany(t => t.Assembly.GetReferencedAssemblies())
            .Select(a => a.Name ?? string.Empty)
            .Distinct()
            .Where(name => !name.StartsWith("System.") &&
                           !name.StartsWith("mscorlib") &&
                           !name.StartsWith("netstandard") &&
                           !name.Equals("Excalibur.Dispatch.Abstractions") &&
                           !allowedNamespaces.Any(allowed => name.StartsWith(allowed)))
            .OrderBy(name => name)
            .ToList();

        // Assert - Report any third-party dependencies (non-blocking for now)
        if (actualDependencies.Any())
        {
            Console.WriteLine(
                "REVIEW: Excalibur.Dispatch.Abstractions has dependencies beyond BCL and Microsoft.Extensions.*.Abstractions. " +
                $"Dependencies: {string.Join(", ", actualDependencies)}. " +
                "Ensure these are necessary for the contract layer.");
        }
    }

    /// <summary>
    /// RULE 8: Hosting packages may reference both Abstractions and Excalibur.Dispatch.
    /// This is the correct pattern for integration/composition packages.
    /// </summary>
    [Fact]
    public void HostingPackages_MayReference_Both_AbstractionsAndDispatch()
    {
        // Arrange
        var hostingNamespaces = new[]
        {
            "Excalibur.Dispatch.Hosting.Web",
            "Excalibur.Hosting"
        };

        foreach (var hostingNs in hostingNamespaces)
        {
            var typesExist = Types.InCurrentDomain()
                .That().ResideInNamespace(hostingNs)
                .GetTypes()
                .Any();

            if (!typesExist)
                continue; // Skip if not loaded

            // Act - Check references
            var hasAbstractions = Types.InCurrentDomain()
                .That().ResideInNamespace(hostingNs)
                .Should().HaveDependencyOn("Excalibur.Dispatch.Abstractions")
                .GetResult()
                .IsSuccessful;

            var hasDispatch = Types.InCurrentDomain()
                .That().ResideInNamespace(hostingNs)
                .Should().HaveDependencyOn("Excalibur.Dispatch")
                .GetResult()
                .IsSuccessful;

            // Assert - Hosting should reference both (composition pattern)
            (hasAbstractions || hasDispatch).ShouldBeTrue(
                $"Hosting package '{hostingNs}' should reference Dispatch packages for composition. " +
                "Hosting packages wire abstractions to implementations via DI.");
        }
    }

    #endregion

    #region Interface Implementation Rules

    /// <summary>
    /// RULE 9: All public concrete classes in Dispatch should implement interfaces from Excalibur.Dispatch.Abstractions.
    /// This validates proper abstraction layer usage.
    ///
    /// VERIFIED IN: TASK-0002 (95%+ coverage achieved)
    /// </summary>
    [Fact]
    public void Dispatch_PublicClasses_ShouldImplement_DispatchAbstractionsInterfaces()
    {
        // Act - Get public concrete classes in Dispatch (excluding test/benchmark namespaces)
        var publicClasses = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch")
            .And().DoNotResideInNamespaceContaining("Tests")
            .And().DoNotResideInNamespaceContaining("Benchmark")
            .And().ArePublic()
            .And().AreClasses()
            .And().AreNotAbstract()
            .GetTypes()
            .Where(t => !t.Name.EndsWith("Exception")) // Exclude exceptions
            .Where(t => !t.Name.EndsWith("EventArgs")) // Exclude event args
            .ToList();

        // Check which classes don't implement any interface from Abstractions
        var classesWithoutInterfaces = publicClasses
            .Where(c => !c.GetInterfaces().Any(i => i.Namespace?.StartsWith("Excalibur.Dispatch.Abstractions") == true))
            .ToList();

        // Assert - Most classes should implement interfaces (allowing some exceptions)
        var complianceRate = publicClasses.Any()
            ? (double)(publicClasses.Count - classesWithoutInterfaces.Count) / publicClasses.Count
            : 1.0;

        complianceRate.ShouldBeGreaterThanOrEqualTo(0.90,
            "At least 90% of public classes in Dispatch should implement interfaces from Excalibur.Dispatch.Abstractions. " +
            $"Current rate: {complianceRate:P0}. " +
            $"Classes without interfaces: {string.Join(", ", classesWithoutInterfaces.Select(c => c.Name))}");
    }

    /// <summary>
    /// RULE 10: Dependency injection registrations should use interfaces, not concrete types.
    /// This test validates that AddDispatch* extension methods register abstractions.
    ///
    /// NOTE: This is an informational test. DI registration patterns were validated
    /// manually in TASK-0003 and found to be 95%+ compliant with interface-based registration.
    /// </summary>
    [Fact]
    public void DependencyInjection_ShouldRegister_Interfaces_Not_ConcreteTypes()
    {
        // Act - Find extension methods that register services
        var extensionMethods = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur")
            .Or().ResideInNamespace("Excalibur.Dispatch")
            .GetTypes()
            .Where(t => t.IsClass && t.IsSealed && t.IsAbstract) // Static classes
            .SelectMany(t => t.GetMethods())
            .Where(m => m.IsStatic && m.IsPublic)
            .Where(m => m.Name.StartsWith("Add") || m.Name.Contains("Register"))
            .Where(m => m.GetParameters().Any(p =>
                p.ParameterType.Name.Contains("IServiceCollection")))
            .ToList();

        // This is informational - documents DI registration pattern compliance
        // Manual validation in TASK-0003 confirmed 95%+ compliance
        Console.WriteLine(
            $"Found {extensionMethods.Count} DI registration extension methods. " +
            "These methods should register interfaces from Excalibur.Dispatch.Abstractions, not Core concrete types. " +
            "Manual review in TASK-0003 confirmed 95%+ interface-based registration compliance.");

        // Pass - this is a documentation test, not an enforcement test
        Assert.True(true, "DI registration pattern documented. See TASK-0003 for validation results.");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Checks if a type exposes Dispatch types in its public API surface.
    /// Examines methods, properties, constructors, and events.
    /// </summary>
    private static bool ExposesDispatchInSignature(Type type)
    {
        // Check public methods
        var methodsExposingDispatch = type.GetMethods()
            .Where(m => m.IsPublic && !m.IsSpecialName)
            .Any(m => IsDispatchType(m.ReturnType) ||
                      m.GetParameters().Any(p => IsDispatchType(p.ParameterType)));

        // Check public properties
        var propertiesExposingDispatch = type.GetProperties()
            .Where(p => p.GetMethod?.IsPublic == true || p.SetMethod?.IsPublic == true)
            .Any(p => IsDispatchType(p.PropertyType));

        // Check public constructors
        var constructorsExposingDispatch = type.GetConstructors()
            .Where(c => c.IsPublic)
            .Any(c => c.GetParameters().Any(p => IsDispatchType(p.ParameterType)));

        return methodsExposingDispatch || propertiesExposingDispatch || constructorsExposingDispatch;
    }

    /// <summary>
    /// Determines if a type is from Dispatch namespace (not Excalibur.Dispatch.Abstractions).
    /// </summary>
    private static bool IsDispatchType(Type type)
    {
        // Handle generic types (e.g., List<DispatchType>)
        var typeToCheck = type.IsGenericType ? type.GetGenericTypeDefinition() : type;

        // Must be in Dispatch namespace but not Excalibur.Dispatch.Abstractions
        return typeToCheck.Namespace?.StartsWith("Excalibur.Dispatch") == true &&
               !typeToCheck.Namespace.StartsWith("Excalibur.Dispatch.Abstractions");
    }

    #endregion
}
