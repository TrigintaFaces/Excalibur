// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using RepositoryChecks;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class ConformanceArmWiringShould
{
    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    public void RequireEveryPolicyToPass(bool contractFailure, bool armFailure, bool guardFailure, bool expected)
    {
        var verdict = new ConformanceCensusVerdict(
            new ConformanceVerdict(contractFailure ? ["new contract"] : [], []),
            armFailure ? ["missing arm"] : [],
            new ConformanceGuardResult([], guardFailure ? ["narrow guard"] : []));
        verdict.Passed.ShouldBe(expected);
    }

    private const string Kit = """
        namespace Contracts {
            public abstract class StoreTestKit {
                protected virtual void Store_Writes() {}
                public virtual void Store_Reads() {}
            }
        }
        """;
    private const string Attributes = """
        namespace Xunit {
            public class FactAttribute : System.Attribute {}
            public class TheoryAttribute : FactAttribute {}
        }
        public class CustomFactAttribute : Xunit.FactAttribute {}
        """;

    [Fact]
    public void ResolveAliasesAndDerivedTestAttributes()
    {
        var suites = Inspect("""
            using Alias = Contracts.StoreTestKit;
            public sealed class Suite : Alias {
                [CustomFact] public void Write() => Store_Writes();
                [Xunit.Theory] public void Read() => Store_Reads();
            }
            """);
        suites.ShouldHaveSingleItem().Arms.ShouldBe(2);
        ConformanceArmWiring.Evaluate(suites).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("// Store_Writes();")]
    [InlineData("var name = \"Store_Writes\";")]
    [InlineData("Other.Store_Writes();")]
    public void RefuseProseStringsAndUnrelatedMethodsAsWiring(string body)
    {
        var suites = Inspect("public sealed class Suite : Contracts.StoreTestKit { [Xunit.Fact] public void Read() { Store_Reads(); " + body +
            "\n } } public static class Other { public static void Store_Writes() {} }");
        suites.ShouldHaveSingleItem().Missing.ShouldHaveSingleItem().ShouldContain("Store_Writes");
        ConformanceArmWiring.Evaluate(suites).ShouldHaveSingleItem();
    }

    [Fact]
    public void IgnoreCallsOutsideDiscoveredTestEntryPoints()
    {
        var suites = Inspect("""
            public sealed class Suite : Contracts.StoreTestKit {
                public void Undiscovered() { Store_Reads(); Store_Writes(); }
            }
            """);
        suites.ShouldHaveSingleItem().Missing.Length.ShouldBe(2);
        suites[0].EntryPoints.ShouldBe(0);
    }

    [Fact]
    public void FollowInheritedWrappersAndSourceHelpers()
    {
        var suites = Inspect("""
            public abstract class Intermediate : Contracts.StoreTestKit {
                [Xunit.Fact] public virtual void Test() => Helper();
                private void Helper() { Store_Reads(); Store_Writes(); }
            }
            public sealed class Suite : Intermediate {}
            """);
        ConformanceArmWiring.Evaluate(suites).ShouldBeEmpty();
    }

    [Fact]
    public void RefuseAnOverriddenWrapperBorrowingItsBaseImplementation()
    {
        var suites = Inspect("""
            public abstract class Intermediate : Contracts.StoreTestKit {
                [Xunit.Fact] public virtual void Test() { Store_Reads(); Store_Writes(); }
            }
            public sealed class Suite : Intermediate {
                [Xunit.Fact] public override void Test() {}
            }
            """);
        suites.ShouldHaveSingleItem().Missing.Length.ShouldBe(2);
    }

    [Fact]
    public void JoinPartialDeclarationsBySymbol()
    {
        var suites = Inspect("""
            public sealed partial class Suite : Contracts.StoreTestKit { [Xunit.Fact] public void Write() => Store_Writes(); }
            public sealed partial class Suite { [Xunit.Fact] public void Read() => Store_Reads(); }
            """);
        ConformanceArmWiring.Evaluate(suites).ShouldBeEmpty();
    }

    [Fact]
    public void DiscoverAnOverrideWithAnInheritedTestAttribute()
    {
        var suites = Inspect("""
            public abstract class Intermediate : Contracts.StoreTestKit {
                [Xunit.Fact] public virtual void Test() {}
            }
            public sealed class Suite : Intermediate {
                public override void Test() { Store_Reads(); Store_Writes(); }
            }
            """);
        suites.ShouldHaveSingleItem().EntryPoints.ShouldBe(1);
        ConformanceArmWiring.Evaluate(suites).ShouldBeEmpty();
    }

    [Fact]
    public void RespectAnAttributeThatDisablesInheritance()
    {
        var suites = Inspect("""
            [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
            public class LocalFactAttribute : Xunit.FactAttribute {}
            public abstract class Intermediate : Contracts.StoreTestKit {
                [LocalFact] public virtual void Test() {}
            }
            public sealed class Suite : Intermediate {
                public override void Test() { Store_Reads(); Store_Writes(); }
            }
            """);
        suites.ShouldHaveSingleItem().EntryPoints.ShouldBe(0);
        suites[0].Missing.Length.ShouldBe(2);
    }

    [Fact]
    public void FollowTheEffectiveVirtualHelperInsteadOfItsHiddenBaseBody()
    {
        var suites = Inspect("""
            public abstract class Intermediate : Contracts.StoreTestKit {
                [Xunit.Fact] public void Test() => Helper();
                protected virtual void Helper() { Store_Reads(); Store_Writes(); }
            }
            public sealed class Suite : Intermediate {
                protected override void Helper() {}
            }
            """);
        suites.ShouldHaveSingleItem().Missing.Length.ShouldBe(2);
    }

    [Fact]
    public void CreditAReferencedOverrideAsTheContractArm()
    {
        var suites = Inspect("""
            public sealed class Suite : Contracts.StoreTestKit {
                public override void Store_Reads() {}
                [Xunit.Fact] public void Test() { Store_Reads(); Store_Writes(); }
            }
            """);
        ConformanceArmWiring.Evaluate(suites).ShouldBeEmpty();
    }

    [Fact]
    public void RefuseAnUnrelatedNewMethodWithTheSameName()
    {
        var suites = Inspect("""
            public sealed class Suite : Contracts.StoreTestKit {
                public new void Store_Reads() {}
                [Xunit.Fact] public void Test() { Store_Reads(); Store_Writes(); }
            }
            """);
        suites.ShouldHaveSingleItem().Missing.ShouldHaveSingleItem().ShouldContain("Store_Reads");
    }

    [Fact]
    public void ReportPrivateProbesSeparatelyWithoutExcludingAnEmptyPublicSuite()
    {
        var suites = Inspect("""
            public class Outer {
                private class PrivateContainer {
                    public sealed class Probe : Contracts.StoreTestKit {}
                }
            }
            public sealed class EmptySuite : Contracts.StoreTestKit {}
            """);
        suites.Single(suite => suite.Name == "Probe").IsHelper.ShouldBeTrue();
        suites.Single(suite => suite.Name == "EmptySuite").IsHelper.ShouldBeFalse();
        ConformanceArmWiring.Evaluate(suites).ShouldHaveSingleItem().ShouldStartWith("Suites:EmptySuite");
    }

    [Fact]
    public void RequireAnExplicitPartialDeferralForAnInternalPrimaryConstructorHelper()
    {
        var suites = Inspect("""
            // conformance-partial-suite: full coverage in Full
            internal sealed class Part(object state) : Contracts.StoreTestKit {}
            public sealed class Full : Contracts.StoreTestKit {
                [Xunit.Fact] public void Test() { Store_Reads(); Store_Writes(); }
            }
            """);
        suites.Single(suite => suite.Name == "Part").IsHelper.ShouldBeFalse();
        ConformanceArmWiring.Evaluate(suites).ShouldBeEmpty();
    }

    [Fact]
    public void BindAPartialMarkerOnlyToItsOwnType()
    {
        var suites = Inspect("""
            // conformance-partial-suite: full coverage in Full
            public sealed class Part : Contracts.StoreTestKit { [Xunit.Fact] public void Read() => Store_Reads(); }
            public sealed class Full : Contracts.StoreTestKit { [Xunit.Fact] public void Test() { Store_Reads(); Store_Writes(); } }
            public sealed class Missing : Contracts.StoreTestKit {}
            """);
        suites.Single(suite => suite.Name == "Part").PartialSibling.ShouldBe("Full");
        suites.Single(suite => suite.Name == "Missing").PartialSibling.ShouldBeNull();
        ConformanceArmWiring.Evaluate(suites).ShouldHaveSingleItem().ShouldStartWith("Suites:Missing");
    }

    [Theory]
    [InlineData("Nowhere")]
    [InlineData("Part")]
    public void RejectMissingOrCircularPartialDeferrals(string sibling)
    {
        var suites = Inspect("// conformance-partial-suite: full coverage in " + sibling +
            "\npublic sealed class Part : Contracts.StoreTestKit {}");
        ConformanceArmWiring.Evaluate(suites).ShouldHaveSingleItem();
    }

    [Fact]
    public void NormalizeGenericArmReferencesAcrossSourceCompilations()
    {
        var suites = Inspect("""
            public sealed class Suite : Contracts.StoreTestKit<string> {
                [Xunit.Fact] public void Test() { Store_Reads(); Store_Writes(); }
            }
            """, Kit.Replace("StoreTestKit", "StoreTestKit<T>", StringComparison.Ordinal));
        ConformanceArmWiring.Evaluate(suites).ShouldBeEmpty();
    }

    [Fact]
    public void RefuseAnEmptyPopulation() => Should.Throw<InvalidOperationException>(() => ConformanceArmWiring.Evaluate([]));

    [Theory]
    [InlineData("Public | Instance", 1)]
    [InlineData("Public | NonPublic | Instance", 0)]
    [InlineData("Public | NonPublic", 1)]
    public void InspectTheFlagsActuallyPassedToTheReflectionGuard(string flags, int expected)
    {
        var source = """
            using System.Linq;
            using static System.Reflection.BindingFlags;
            public abstract class GuardTestKit {
                protected virtual void Store_Writes() {}
                public virtual void ConformanceSuite_ShouldWireEveryArm() {
                    const System.Reflection.BindingFlags Unused = Public | NonPublic | Instance;
                    const System.Reflection.BindingFlags Actual = FLAGS;
                    _ = GetType().GetMethods(Actual).Where(method => method.IsVirtual);
                    _ = GetType().GetMethods(Public | Instance);
                }
            }
            """.Replace("FLAGS", flags, StringComparison.Ordinal);
        var compilation = CSharpCompilation.Create("Published", [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
             MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
             MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Runtime").Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var result = ConformanceGuardRule.Analyze(compilation, compilation.Assembly);
        result.GuardlessKits.ShouldBeEmpty();
        result.Failures.Length.ShouldBe(expected);
    }

    [Fact]
    public void ReportGuardlessKitsWithoutPretendingTheyHaveRuntimeCoverage()
    {
        var compilation = CSharpCompilation.Create("Published", [CSharpSyntaxTree.ParseText(Kit)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var result = ConformanceGuardRule.Analyze(compilation, compilation.Assembly);
        result.GuardlessKits.ShouldHaveSingleItem().ShouldBe("Contracts.StoreTestKit");
        result.Failures.ShouldBeEmpty();
    }

    private static IReadOnlyList<ArmSuite> Inspect(string source, string kit = Kit)
    {
        var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        var published = CSharpCompilation.Create("Published", [CSharpSyntaxTree.ParseText(kit)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        published.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var reference = published.ToMetadataReference();
        var compilation = CSharpCompilation.Create("Suites", [CSharpSyntaxTree.ParseText(source + "\n" + Attributes)],
            references.Append<MetadataReference>(reference), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var assembly = compilation.GetAssemblyOrModuleSymbol(reference).ShouldBeAssignableTo<IAssemblySymbol>();
        return ConformanceArmWiring.Analyze(compilation, assembly);
    }
}
