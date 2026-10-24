// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RepositoryChecks;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class TenantMutationRuleShould
{
    private const string Attributes = """
        namespace Xunit {
            public class FactAttribute : System.Attribute {}
            public sealed class TheoryAttribute : FactAttribute {}
            public sealed class MemberDataAttribute : System.Attribute {
                public MemberDataAttribute(string name) {}
                public System.Type MemberType { get; set; }
            }
        }
        """;

    [Theory]
    [InlineData("public sealed class Request { public string Sql => \"DELETE FROM items\"; public Excalibur.Dispatch.TenantScope Scope; }", true)]
    [InlineData("public sealed class Request { public string Sql => $\"UPDATE {1} SET x = 1\"; public Excalibur.Dispatch.KeyedTenantPartition Scope; }", true)]
    [InlineData("public partial class Request { public string Sql => \"DELETE FROM items\"; } public partial class Request { public Excalibur.Dispatch.TenantScope Scope; }", true)]
    [InlineData("public abstract class Parent { public string Sql => \"DELETE FROM items\"; public Excalibur.Dispatch.TenantScope Scope; } public sealed class Request : Parent {}", true)]
    [InlineData("public sealed class Request { public string Sql => Helper(); private static string Helper() => \"DELETE FROM items\"; public Excalibur.Dispatch.TenantScope Scope; }", true)]
    [InlineData("public sealed class Request { public const string Sql = \"DELETE FROM items\"; }", false)]
    [InlineData("namespace Other { public struct TenantScope {} } public sealed class Request { public const string Sql = \"DELETE FROM items\"; public Other.TenantScope Scope; }", false)]
    public void DiscoverResolvedMutationAndTenantTypes(string source, bool scoped)
    {
        var tenant = Tenant();
        var compilation = Compile("Provider", source, "src/Excalibur/Excalibur.EventSourcing.SqlServer/Requests/Request.cs", tenant.ToMetadataReference());
        var mutation = TenantMutationRule.Find(compilation, tenant.Assembly, Directory.GetCurrentDirectory()).ShouldHaveSingleItem();
        mutation.Scoped.ShouldBe(scoped);
    }

    [Fact]
    public void IgnoreCommentOnlySql()
    {
        var tenant = Tenant();
        var compilation = Compile("Provider", "public sealed class Request { /* DELETE FROM items */ }", "src/Excalibur/Excalibur.EventSourcing.SqlServer/Requests/Request.cs", tenant.ToMetadataReference());
        TenantMutationRule.Find(compilation, tenant.Assembly, Directory.GetCurrentDirectory()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("[Xunit.Fact] public void Test() { _ = new Request(); }", 1)]
    [InlineData("[Xunit.Fact] public void Test() { Make(); } private static Request Make() => new();", 1)]
    [InlineData("[Xunit.Fact] public void Test() {} private static Request Unused() => new();", 0)]
    [InlineData("[Xunit.Fact] public void Test() { Request Unused() => new(); }", 0)]
    [InlineData("[Xunit.Fact] public void Test() { System.Func<Request> unused = () => new(); }", 0)]
    [InlineData("[Xunit.Theory, Xunit.MemberData(nameof(Data))] public void Test(object value) {} public static object[] Data() => new object[] { new Request() };", 1)]
    [InlineData("public void NotATest() { _ = new Request(); }", 0)]
    public void RequireReachableTestOrMemberDataConstruction(string body, int expected) => Bindings("public sealed class Suite { " + body + " }").Length.ShouldBe(expected);

    [Fact]
    public void RefuseAnOverriddenTestBody() => Bindings("""
        public abstract class Parent { [Xunit.Fact] public virtual void Test() { _ = new Request(); } }
        public sealed class Suite : Parent { public override void Test() {} }
        """).ShouldBeEmpty();

    [Fact]
    public void RefuseHiddenBaseMemberData() => Bindings("""
        public abstract class Parent { public static object[] Data() => new object[] { new Request() }; }
        public sealed class Suite : Parent {
            public new static object[] Data() => new object[] { 1 };
            [Xunit.Theory, Xunit.MemberData(nameof(Data))] public void Test(object value) {}
        }
        """).ShouldBeEmpty();

    [Theory]
    [InlineData("public sealed class Request { const string Sql = \"DELETE FROM items\"; }", "src/Requests/Request.cs", true)]
    [InlineData("public sealed class Request {}", "src/Requests/Request.cs", true)]
    [InlineData("public sealed class Helper { const string Sql = \"DELETE FROM items\"; }", "src/Helper.cs", true)]
    [InlineData("internal static class MissingTypesResolution {}", "tests/Shared/MissingTypesResolution.cs", false)]
    public void RefuseExcludedEvidence(string source, string path, bool expected) => TenantMutationCensus.ExcludedSourceContainsEvidence(source, path).ShouldBe(expected);

    [Fact]
    public void RefuseAnEmptyInventory() => TenantMutationCensus.Evaluate([], []).Length.ShouldBe(7);

    private static string[] Bindings(string source)
    {
        var compilation = Compile("Tests", "public sealed class Request {} " + Attributes + source, "tests/TenantConformance.cs");
        return TenantMutationRule.Bindings(compilation, [new BackendProvider(compilation.GetTypeByMetadataName("Request")!)]);
    }

    private static CSharpCompilation Tenant() => Compile("Tenant", "namespace Excalibur.Dispatch { public struct TenantScope {} public struct KeyedTenantPartition {} }", "src/Tenant.cs");

    private static CSharpCompilation Compile(string assembly, string source, string path, params MetadataReference[] references)
    {
        var compilation = CSharpCompilation.Create(assembly, [CSharpSyntaxTree.ParseText(source, path: Path.GetFullPath(path))],
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) }.Concat<MetadataReference>(references),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        return compilation;
    }
}
