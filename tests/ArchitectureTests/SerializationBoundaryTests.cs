using NetArchTest.Rules;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.ArchitectureTests;

/// <summary>
/// Enforces serialization boundary requirements for Excalibur.Dispatch.Patterns (R0.14).
/// </summary>
public sealed class SerializationBoundaryTests
{
    /// <summary>
    /// Excalibur.Dispatch.Patterns core must not depend on System.Text.Json; JSON providers live in hosting/public edge packages.
    /// </summary>
    [Fact]
    public void DispatchPatterns_ShouldNotReference_SystemTextJson()
    {
        var result = Types.InCurrentDomain()
            .That().ResideInNamespace("Excalibur.Dispatch.Patterns")
            .ShouldNot().HaveDependencyOn("System.Text.Json")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            "Excalibur.Dispatch.Patterns must not reference System.Text.Json per R0.14; JSON providers belong in Excalibur.Dispatch.Patterns.Hosting.Json or other edge packages. " +
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }
}
