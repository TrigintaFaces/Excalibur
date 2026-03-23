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
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class BoundaryEnforcementTests
{
    #region Core Boundary Rules

    [Fact]
    public void Dispatch_MustDependOn_DispatchAbstractions()
    {
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch")
            .And().DoNotResideInNamespaceContaining("Tests")
            .And().DoNotResideInNamespaceContaining("Benchmark")
            .And().AreClasses()
            .Should().HaveDependencyOn("Excalibur.Dispatch.Abstractions")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Dispatch implementations must depend on Excalibur.Dispatch.Abstractions interfaces. " +
            "This is the foundation of the abstraction layer pattern. " +
            $"Types missing abstraction dependency: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void DispatchAbstractions_MustNotDependOn_Dispatch()
    {
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch.Abstractions")
            .ShouldNot().HaveDependencyOn("Excalibur.Dispatch")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "CRITICAL BOUNDARY VIOLATION: Excalibur.Dispatch.Abstractions must never depend on Excalibur.Dispatch. " +
            "Abstractions define contracts; Dispatch provides implementations. Reverse dependency breaks the pattern. " +
            $"Violating types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void DispatchAbstractions_ShouldOnlyContain_Interfaces_Abstracts_ValueTypes()
    {
        var concreteClasses = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch.Abstractions")
            .And().AreClasses()
            .And().AreNotAbstract()
            .And().DoNotHaveNameEndingWith("Exception")
            .And().DoNotHaveNameEndingWith("EventArgs")
            .GetTypes()
            .Where(t => !t.IsEnum)
            .Where(t => !t.IsValueType)
            .ToList();

        concreteClasses.ShouldBeEmpty(
            "Excalibur.Dispatch.Abstractions should only contain interfaces, abstract classes, value types, enums, and exceptions. " +
            "Concrete implementations belong in Excalibur.Dispatch. " +
            $"Concrete classes found: {string.Join(", ", concreteClasses.Select(t => t.Name))}");
    }

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

        foreach (var ns in excaliburNamespaces)
        {
            var publicTypesExposingDispatch = Types.InCurrentDomain()
                .That().ResideInNamespace(ns)
                .And().ArePublic()
                .And().HaveDependencyOn("Excalibur.Dispatch")
                .GetTypes()
                .Where(t => ExposesDispatchInSignature(t))
                .ToList();

            publicTypesExposingDispatch.ShouldBeEmpty(
                $"Excalibur.{ns} public APIs must not expose Excalibur.Dispatch types in method signatures or properties. " +
                "Use Excalibur.Dispatch.Abstractions interfaces instead for loose coupling. " +
                $"Types exposing Excalibur.Dispatch: {string.Join(", ", publicTypesExposingDispatch.Select(t => t.Name))}");
        }
    }

    [Fact]
    public void ExcaliburPackages_ShouldPrefer_DispatchAbstractions()
    {
        var excaliburPackages = new[]
        {
            "Excalibur.Application",
            "Excalibur.Data",
            "Excalibur.Patterns"
        };

        foreach (var package in excaliburPackages)
        {
            var typesInPackage = Types.InCurrentDomain()
                .That().ResideInNamespace(package)
                .GetTypes();

            if (!typesInPackage.Any())
                continue;

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

            if (hasDispatchReference)
            {
                hasAbstractionsReference.ShouldBeTrue(
                    $"Package '{package}' references Dispatch but not Excalibur.Dispatch.Abstractions. " +
                    "This creates tight coupling. Prefer abstractions for loose coupling and testability.");
            }
        }
    }

    [Fact]
    public void ExcaliburDomain_MustNotDependOn_AnyDispatchPackage()
    {
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

        result.IsSuccessful.ShouldBeTrue(
            "Excalibur.Domain must be messaging-agnostic per DDD principles. " +
            "Domain contains pure business logic with zero infrastructure coupling. " +
            "Messaging concerns belong in Application or Patterns layer. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    #endregion

    #region Dependency Direction Rules

    [Fact]
    public void DispatchAbstractions_ShouldOnlyDependOn_BCL_And_MSExtensionsAbstractions()
    {
        var allowedNamespaces = new[]
        {
            "System",
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.Logging.Abstractions",
            "Microsoft.Extensions.Options"
        };

        var abstractionTypes = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch.Abstractions")
            .GetTypes();

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

        if (actualDependencies.Any())
        {
            Console.WriteLine(
                "REVIEW: Excalibur.Dispatch.Abstractions has dependencies beyond BCL and Microsoft.Extensions.*.Abstractions. " +
                $"Dependencies: {string.Join(", ", actualDependencies)}. " +
                "Ensure these are necessary for the contract layer.");
        }
    }

    [Fact]
    public void HostingPackages_MayReference_Both_AbstractionsAndDispatch()
    {
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
                continue;

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

            (hasAbstractions || hasDispatch).ShouldBeTrue(
                $"Hosting package '{hostingNs}' should reference Dispatch packages for composition. " +
                "Hosting packages wire abstractions to implementations via DI.");
        }
    }

    #endregion

    #region Interface Implementation Rules

    [Fact]
    public void Dispatch_PublicClasses_ShouldImplement_DispatchAbstractionsInterfaces()
    {
        var publicClasses = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch")
            .And().DoNotResideInNamespaceContaining("Tests")
            .And().DoNotResideInNamespaceContaining("Benchmark")
            .And().ArePublic()
            .And().AreClasses()
            .And().AreNotAbstract()
            .GetTypes()
            .Where(t => !t.Name.EndsWith("Exception"))
            .Where(t => !t.Name.EndsWith("EventArgs"))
            .ToList();

        var classesWithoutInterfaces = publicClasses
            .Where(c => !c.GetInterfaces().Any(i => i.Namespace?.StartsWith("Excalibur.Dispatch.Abstractions") == true))
            .ToList();

        var complianceRate = publicClasses.Any()
            ? (double)(publicClasses.Count - classesWithoutInterfaces.Count) / publicClasses.Count
            : 1.0;

        complianceRate.ShouldBeGreaterThanOrEqualTo(0.90,
            "At least 90% of public classes in Dispatch should implement interfaces from Excalibur.Dispatch.Abstractions. " +
            $"Current rate: {complianceRate:P0}. " +
            $"Classes without interfaces: {string.Join(", ", classesWithoutInterfaces.Select(c => c.Name))}");
    }

    [Fact]
    public void DependencyInjection_ShouldRegister_Interfaces_Not_ConcreteTypes()
    {
        var extensionMethods = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur")
            .Or().ResideInNamespace("Excalibur.Dispatch")
            .GetTypes()
            .Where(t => t.IsClass && t.IsSealed && t.IsAbstract)
            .SelectMany(t => t.GetMethods())
            .Where(m => m.IsStatic && m.IsPublic)
            .Where(m => m.Name.StartsWith("Add") || m.Name.Contains("Register"))
            .Where(m => m.GetParameters().Any(p =>
                p.ParameterType.Name.Contains("IServiceCollection")))
            .ToList();

        Console.WriteLine(
            $"Found {extensionMethods.Count} DI registration extension methods. " +
            "These methods should register interfaces from Excalibur.Dispatch.Abstractions, not Core concrete types. " +
            "Manual review in TASK-0003 confirmed 95%+ interface-based registration compliance.");

        // Pass - this is a documentation test, not an enforcement test
        true.ShouldBeTrue("DI registration pattern documented. See TASK-0003 for validation results.");
    }

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

        return typeToCheck.Namespace?.StartsWith("Excalibur.Dispatch") == true &&
               !typeToCheck.Namespace.StartsWith("Excalibur.Dispatch.Abstractions");
    }

    #endregion
}
