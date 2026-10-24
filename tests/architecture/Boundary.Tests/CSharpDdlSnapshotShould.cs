// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using RepositoryChecks;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class CSharpDdlSnapshotShould
{
    [Fact]
    public void PreserveQualifiedNestedPropertyOwnersAndAliases()
    {
        using var facts = JsonDocument.Parse(CSharpDdlFacts.Read("""
            using Alias = Example.Outer.Options;
            namespace Example {
                public class Outer { public class Options { public string TableName { get; set; } = "A"; } }
                public class Other { public class Options { public string TableName => "B"; } }
                public class Store { private Alias _options; }
            }
            """));
        facts.RootElement.GetProperty("properties").EnumerateArray().Select(property => property.GetProperty("owner").GetString())
            .ShouldBe(["Example.Outer.Options", "Example.Other.Options"]);
        facts.RootElement.GetProperty("fields")[0].GetProperty("type").GetString().ShouldBe("Example.Outer.Options");
    }

    [Theory]
    [InlineData("string Ignored() => \"sys.columns\";")]
    [InlineData("var unused = nameof(ColumnProbe);")]
    [InlineData("System.Func<string> unused = () => \"sys.columns\";")]
    public void DoNotBorrowColumnProofFromUnexecutedCode(string unused)
    {
        using var facts = JsonDocument.Parse(CSharpDdlFacts.Read("""
            class Store {
                string ColumnProbe() => "sys.columns";
                string VerifySchema() { UNUSED return "sys.tables"; }
            }
            """.Replace("UNUSED", unused, StringComparison.Ordinal)));
        facts.RootElement.GetProperty("verifications")[0].GetString().ShouldBe("sys.tables");
    }

    [Fact]
    public void FollowActuallyInvokedLocalHelpers()
    {
        using var facts = JsonDocument.Parse(CSharpDdlFacts.Read("""
            class Store {
                string ColumnProbe() => "sys.columns";
                string VerifySchema() => "sys.tables" + ColumnProbe();
            }
            """));
        facts.RootElement.GetProperty("verifications")[0].GetString().ShouldContain("sys.columns");
    }

    [Fact]
    public void ExcludeCommentOnlyDdl()
    {
        using var facts = JsonDocument.Parse(CSharpDdlFacts.Read("// CREATE TABLE Fake (Id int);\nclass Store {}"));
        facts.RootElement.GetProperty("sql").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public void PreserveUnknownPropertyDefaults()
    {
        using var facts = JsonDocument.Parse(CSharpDdlFacts.Read("class Options { public string TableName => Unknown(); }"));
        facts.RootElement.GetProperty("properties")[0].GetProperty("value").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public void RefuseMalformedSource() => Should.Throw<InvalidOperationException>(() => CSharpDdlFacts.Read("class {"));
}
