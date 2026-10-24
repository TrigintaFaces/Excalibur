// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RepositoryChecks;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class BackendBindingRuleShould
{
    private const string Contracts = """
        public sealed class First {}
        public sealed class Second {}
        namespace Microsoft.Extensions.DependencyInjection {
            public static class Services {
                public static object GetRequiredService<T>() => new object();
                public static object AddSingleton<T>() => new object();
            }
        }
        """;

    [Fact]
    public void ResolveConstructionAndTypeOfRatherThanProse()
    {
        Inspect("""
            public sealed class Suite : Trusted.Kit {
                public object Make() => new First();
                public System.Type Type() => typeof(First);
                private string Text = "new Second(); typeof(Second)";
            }
            """).ShouldBe(["Fixture:First"]);
    }

    [Fact]
    public void IgnoreUnrelatedAndNestedTypesInTheSameFile()
    {
        Inspect("""
            public sealed class Suite : Trusted.Kit {
                private sealed class Nested { public object Make() => new First(); }
            }
            public sealed class Unrelated { public object Make() => new Second(); }
            """).ShouldBeEmpty();
    }

    [Fact]
    public void ResolveGenericDependencyInjectionReferencesThroughRecursiveHelpers()
    {
        Inspect("""
            public static class Registration {
                public static void Add() { Helper<First>(); }
                private static void Helper<T>() { Recursive<T>(); }
                private static void Recursive<T>() {
                    Microsoft.Extensions.DependencyInjection.Services.AddSingleton<T>();
                    Helper<T>();
                }
            }
            public sealed class Suite : Trusted.Kit { public void Configure() => Registration.Add(); }
            """).ShouldBe(["Fixture:First"]);
    }

    [Fact]
    public void RefuseUnusedGenericArgumentsAsRegistrationEvidence()
    {
        Inspect("""
            public static class Registration { public static void Add<T>() {} }
            public sealed class Suite : Trusted.Kit { public void Configure() => Registration.Add<First>(); }
            """).ShouldBeEmpty();
    }

    [Fact]
    public void RefuseAnInitiallyUniqueRegistrationThatBecomesAmbiguous()
    {
        Inspect("""
            public static class Registration {
                public static void Add() { _ = new First(); Later(); }
                private static void Later() { _ = new Second(); }
            }
            public sealed class Suite : Trusted.Kit { public void Configure() => Registration.Add(); }
            """).ShouldBeEmpty();
    }

    [Fact]
    public void ResolveTheCalledOverloadInsteadOfItsBareName()
    {
        Inspect("""
            public static class Registration {
                public static void Add(int value) { _ = new First(); }
                public static void Add(string value) { _ = new Second(); }
            }
            public sealed class Suite : Trusted.Kit { public void Configure() => Registration.Add(1); }
            """).ShouldBe(["Fixture:First"]);
    }

    [Fact]
    public void ResolveExtensionRegistrationCalls()
    {
        Inspect("""
            public static class Registration {
                public static void Add(this object services) { _ = new First(); }
            }
            public sealed class Suite : Trusted.Kit { public void Configure() => new object().Add(); }
            """).ShouldBe(["Fixture:First"]);
    }

    [Fact]
    public void RespectPartialSuitesAndPrimaryConstructors()
    {
        Inspect("""
            public sealed partial class Suite(int value) : Trusted.Kit { public int Value => value; }
            public sealed partial class Suite { public object Make() => new First(); }
            """).ShouldBe(["Fixture:First"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KeepRegistrationClosuresInsideTheirProviderAssembly(bool ownConstruction)
    {
        var published = Compile("Published", "namespace Trusted { public abstract class Kit {} }");
        var other = Compile("Other", "public sealed class Second {} public static class OtherRegistration { public static void Add() { _ = new Second(); } }");
        var provider = Compile("Provider", "public sealed class First {} public static class Registration { public static void Add() { " +
            (ownConstruction ? "_ = new First();" : "") + " OtherRegistration.Add(); } }", other.ToMetadataReference());
        var consumer = Compile("Consumer", "public sealed class Suite : Trusted.Kit { public void Configure() => Registration.Add(); }",
            published.ToMetadataReference(), provider.ToMetadataReference(), other.ToMetadataReference());
        var bindings = BackendBindingRule.Bindings(consumer, consumer.GetTypeByMetadataName("Trusted.Kit")!,
            [new BackendProvider(provider.GetTypeByMetadataName("First")!), new BackendProvider(other.GetTypeByMetadataName("Second")!)]);
        bindings.ShouldBe(ownConstruction ? ["Provider:First"] : []);
    }

    [Fact]
    public void RefuseAnotherContractsKitEvenWhenTheBackendImplementsBoth()
    {
        var published = Compile("Published", "namespace Trusted { public abstract class FirstKit {} public abstract class SecondKit {} }");
        var consumer = Compile("Consumer", "public sealed class SharedBackend {} public sealed class Suite : Trusted.FirstKit { public object Make() => new SharedBackend(); }",
            published.ToMetadataReference());
        BackendProvider[] providers = [new(consumer.GetTypeByMetadataName("SharedBackend")!)];
        BackendBindingRule.Bindings(consumer, consumer.GetTypeByMetadataName("Trusted.FirstKit")!, providers).ShouldHaveSingleItem();
        BackendBindingRule.Bindings(consumer, consumer.GetTypeByMetadataName("Trusted.SecondKit")!, providers).ShouldBeEmpty();
    }

    [Fact]
    public void RejectSameNamedForeignKits()
    {
        var published = Compile("Published", "namespace Trusted { public abstract class Kit {} }");
        var consumer = Compile("Consumer", "namespace Foreign { public abstract class Kit {} } public sealed class First {} public sealed class Suite : Foreign.Kit { public object Make() => new First(); }",
            published.ToMetadataReference());
        BackendBindingRule.Bindings(consumer, consumer.GetTypeByMetadataName("Trusted.Kit")!,
            [new BackendProvider(consumer.GetTypeByMetadataName("First")!)]).ShouldBeEmpty();
    }

    [Fact]
    public void ResolveGenericContainingTypeRegistrations()
    {
        Inspect("""
            public static class Registration<T> {
                public static void Add() => Microsoft.Extensions.DependencyInjection.Services.GetRequiredService<T>();
            }
            public sealed class Suite : Trusted.Kit { public void Configure() => Registration<First>.Add(); }
            """).ShouldBe(["Fixture:First"]);
    }

    [Fact]
    public void ResolveInheritedGenericSuiteConstruction()
    {
        Inspect("""
            public abstract class Intermediate<T> : Trusted.Kit where T : new() { public object Make() => new T(); }
            public sealed class Suite : Intermediate<First> {}
            """).ShouldBe(["Fixture:First"]);
    }

    [Theory]
    [InlineData("public object Make() => new First();")]
    [InlineData("public void Configure() => Registration.Add();")]
    public void RejectAProviderAssemblyImpostorWithIdenticalDisplayNames(string body)
    {
        var published = Compile("Published", "namespace Trusted { public abstract class Kit {} }");
        var trusted = Compile("Provider", "public sealed class First {}");
        var impostor = Compile("Provider", "public sealed class First {} public static class Registration { public static void Add() { _ = new First(); } } public sealed class Suite : Trusted.Kit { " + body + " }",
            published.ToMetadataReference());
        BackendBindingRule.Bindings(impostor, impostor.GetTypeByMetadataName("Trusted.Kit")!,
            [new BackendProvider(trusted.GetTypeByMetadataName("First")!)]).ShouldBeEmpty();
    }

    [Fact]
    public void FollowOnlyTheInvokedConcreteFactoryMethod()
    {
        Inspect("""
            public sealed class Factory {
                public object Create() => new First();
                public object Unused() => new Second();
            }
            public sealed class Suite : Trusted.Kit { public object Make() => new Factory().Create(); }
            """).ShouldBe(["Fixture:First"]);
    }

    [Fact]
    public void RefuseToInferAnInterfaceFactoryImplementation()
    {
        Inspect("""
            public interface Factory { object Create(); }
            public sealed class Concrete : Factory { public object Create() => new First(); }
            public sealed class Suite : Trusted.Kit { public object Make(Factory factory) => factory.Create(); }
            """).ShouldBeEmpty();
    }

    [Fact]
    public void RefuseToInferVirtualDispatchFromTheDeclaredReceiver()
    {
        Inspect("""
            public class Factory { public virtual object Create() => new First(); }
            public sealed class Replacement : Factory { public override object Create() => new Second(); }
            public sealed class Suite : Trusted.Kit { public object Make(Factory factory) => factory.Create(); }
            """).ShouldBeEmpty();
    }

    private static CSharpCompilation Compile(string name, string source, params MetadataReference[] references)
    {
        var compilation = CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source)],
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) }.Concat<MetadataReference>(references),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        return compilation;
    }

    private static string[] Inspect(string source)
    {
        var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        var published = CSharpCompilation.Create("Published", [CSharpSyntaxTree.ParseText("namespace Trusted { public abstract class Kit {} }")],
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var compilation = CSharpCompilation.Create("Fixture", [CSharpSyntaxTree.ParseText(Contracts + source)],
            references.Append<MetadataReference>(published.ToMetadataReference()), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        return BackendBindingRule.Bindings(compilation, compilation.GetTypeByMetadataName("Trusted.Kit")!,
            [new BackendProvider(compilation.GetTypeByMetadataName("First")!), new BackendProvider(compilation.GetTypeByMetadataName("Second")!)]);
    }
}
