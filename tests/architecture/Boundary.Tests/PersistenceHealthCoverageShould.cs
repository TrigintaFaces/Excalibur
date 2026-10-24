// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using RepositoryChecks;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class PersistenceHealthCoverageShould
{
    private const string Contracts = """
        namespace Trusted {
            public interface Health {}
            public abstract class Kit { protected abstract object CreateProvider(string name); }
        }
        public abstract class Base : Trusted.Health {}
        public sealed class First : Base {}
        public sealed class Second : Trusted.Health {}
        """;

    [Fact]
    public void InventoryInheritedImplementationsIndependentlyOfTestReferences()
    {
        var compilation = Compile(Contracts);
        var providers = PersistenceHealthCoverage.Providers(compilation, compilation.GetTypeByMetadataName("Trusted.Health")!);
        providers.ShouldBe(["Fixture:First", "Fixture:Second"]);
        PersistenceHealthCoverage.Evaluate(providers, ["Fixture:First"]).ShouldBe(["Fixture:Second"]);
    }

    [Fact]
    public void ResolveAliasesAndTargetTypedConstruction()
    {
        Inspect("""
            public sealed class Suite : Trusted.Kit {
                protected override object CreateProvider(string name) { First value = new(); return value; }
            }
            """).ShouldBe(["Fixture:First"]);
    }

    [Fact]
    public void IgnoreUnrelatedMethodsNestedTypesAndProse()
    {
        Inspect("""
            public sealed class Suite : Trusted.Kit {
                protected override object CreateProvider(string name) => new object();
                private object Unused() => new First();
                private sealed class Nested { public object Make() => new Second(); }
                // new First();
            }
            """).ShouldBeEmpty();
    }

    [Fact]
    public void RefuseWrongBackendAndOverriddenFactoryBorrowingBaseConstruction()
    {
        var bindings = Inspect("""
            public abstract class Intermediate : Trusted.Kit {
                protected override object CreateProvider(string name) => new First();
            }
            public sealed class Suite : Intermediate {
                protected override object CreateProvider(string name) => new Second();
            }
            """);
        bindings.ShouldBe(["Fixture:Second"]);
        PersistenceHealthCoverage.Evaluate(["Fixture:First", "Fixture:Second"], bindings).ShouldBe(["Fixture:First"]);
    }

    [Fact]
    public void IncludeInheritedFactoryConstruction()
    {
        Inspect("""
            public abstract class Intermediate : Trusted.Kit {
                protected override object CreateProvider(string name) => new First();
            }
            public sealed class Suite : Intermediate {}
            """).ShouldBe(["Fixture:First"]);
    }

    [Fact]
    public void RejectSameNamedForeignContractsAndKits()
    {
        Inspect("""
            namespace Foreign {
                public interface Health {}
                public sealed class Provider : Health {}
                public abstract class Kit { protected abstract object CreateProvider(string name); }
                public sealed class WrongKit : Kit { protected override object CreateProvider(string name) => new First(); }
            }
            public sealed class WrongProvider : Trusted.Kit {
                protected override object CreateProvider(string name) => new Foreign.Provider();
            }
            """).ShouldBeEmpty();
    }

    [Fact]
    public void RefuseAnEmptyProviderPopulation() =>
        Should.Throw<InvalidOperationException>(() => PersistenceHealthCoverage.Evaluate([], ["Fixture:First"]));

    [Fact]
    public void ResolveInheritedFactoriesAcrossRetargetedSourceCompilations()
    {
        var original = Compile("[assembly: System.Reflection.AssemblyVersion(\"1.0.0.0\")] namespace Dependency; public class Value {}")
            .WithAssemblyName("Dependency");
        var replacement = Compile("[assembly: System.Reflection.AssemblyVersion(\"2.0.0.0\")] namespace Dependency; public class Value {}")
            .WithAssemblyName("Dependency");
        var contracts = Compile("""
            namespace Trusted {
                public interface Health { Dependency.Value GetValue(); }
                public abstract class Kit { protected abstract object CreateProvider(string name); }
            }
            """, original.ToMetadataReference()).WithAssemblyName("Contracts");
        var helper = Compile("""
            public sealed class Provider : Trusted.Health { public Dependency.Value GetValue() => new(); }
            public abstract class Helper : Trusted.Kit {
                protected override object CreateProvider(string name) => new Provider();
            }
            """, contracts.ToMetadataReference(), original.ToMetadataReference()).WithAssemblyName("Helpers");
        var consumer = Compile("public sealed class Suite : Helper {}", helper.ToMetadataReference(),
            contracts.ToMetadataReference(), replacement.ToMetadataReference()).WithAssemblyName("Consumer");
        var visible = PublishedContracts.ResolveReference(consumer, contracts.Assembly)!;
        SymbolEqualityComparer.Default.Equals(visible, contracts.Assembly).ShouldBeFalse("The fixture must exercise retargeting.");
        PersistenceHealthCoverage.Bindings(consumer, visible.GetTypeByMetadataName("Trusted.Kit")!,
            [new BackendProvider(helper.GetTypeByMetadataName("Provider")!)]).ShouldBe(["Helpers:Provider"]);
    }

    [Theory]
    [InlineData("#if OTHER\nclass Hidden : System.IDisposable { public void Dispose() {} }\n#endif")]
    [InlineData("#if OTHER\nglobal using Health = System.IDisposable;\n#endif")]
    [InlineData("using Trusted;\n#if !OTHER\ninterface Health {}\n#endif\nclass Provider : Health {}\nnamespace Trusted { public interface Health {} }")]
    [InlineData("#if OTHER\nnamespace Other { class Provider : System.IDisposable { public void Dispose() {} } }\n#else\nnamespace Current { class Provider : System.IDisposable { public void Dispose() {} } }\n#endif")]
    public void RefuseUnevaluatedShippingInheritance(string source)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, source);
            var compilation = Compile("").AddSyntaxTrees(CSharpSyntaxTree.ParseText(source, path: path));
            Should.Throw<InvalidOperationException>(() => new CompilationSourceCoverage().Add(compilation));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("class Missing : System.IDisposable { public void Dispose() {} }")]
    [InlineData("partial class Missing {}")]
    public void RefuseExcludedProviderDeclarations(string source)
    {
        var root = Path.Combine(Path.GetTempPath(), "health-coverage-" + Guid.NewGuid().ToString("N"));
        var directory = Directory.CreateDirectory(Path.Combine(root, "src"));
        var path = Path.Combine(directory.FullName, "Missing.cs");
        try
        {
            File.WriteAllText(path, source);
            Should.Throw<InvalidOperationException>(() => new CompilationSourceCoverage().Verify(root, "src"));
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(directory.FullName);
            Directory.Delete(root);
        }
    }

    [Fact]
    public void RejectAProviderAssemblyImpostorWithIdenticalDisplayNames()
    {
        var published = Compile("namespace Trusted { public abstract class Kit { protected abstract object CreateProvider(string name); } }")
            .WithAssemblyName("Published");
        var trusted = Compile("public sealed class Provider {}").WithAssemblyName("ProviderAssembly");
        var impostor = Compile("""
            public sealed class Provider {}
            public sealed class Suite : Trusted.Kit {
                protected override object CreateProvider(string name) => new Provider();
            }
            """, published.ToMetadataReference()).WithAssemblyName("ProviderAssembly");
        PersistenceHealthCoverage.Bindings(impostor, impostor.GetTypeByMetadataName("Trusted.Kit")!,
            [new BackendProvider(trusted.GetTypeByMetadataName("Provider")!)]).ShouldBeEmpty();
    }

    private static string[] Inspect(string source)
    {
        var compilation = Compile(Contracts + source);
        return PersistenceHealthCoverage.Bindings(compilation, compilation.GetTypeByMetadataName("Trusted.Kit")!,
            PersistenceHealthCoverage.ProviderTypes(compilation, compilation.GetTypeByMetadataName("Trusted.Health")!).Select(type => new BackendProvider(type)).ToArray());
    }

    private static CSharpCompilation Compile(string source, params MetadataReference[] references)
    {
        var compilation = CSharpCompilation.Create("Fixture", [CSharpSyntaxTree.ParseText(source)],
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) }.Concat<MetadataReference>(references),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        return compilation;
    }
}
