// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using RepositoryChecks;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class CSharpSqlStringsShould
{
    [Theory]
    [InlineData("var sql = \"UPDATE {table} SET value = @Value\";")]
    [InlineData("var sql = @\"UPDATE {table} SET value = @Value\";")]
    [InlineData("var sql = $\"UPDATE {table} SET value = @Value\";")]
    [InlineData("var sql = \"UPDATE {table} \" + \"SET value = @Value\";")]
    public void DecodeSupportedStringForms(string source) =>
        CSharpSqlStrings.Read(source).ShouldContain("UPDATE {table} SET value = @Value");

    [Fact]
    public void DecodeRawInterpolatedSql() => CSharpSqlStrings.Read(""""
        var sql = $"""
            UPDATE {table}
            SET value = @Value
            """;
        """").ShouldContain("UPDATE {table}\nSET value = @Value");

    [Fact]
    public void IgnoreCommentsAndCharacterLiterals() =>
        CSharpSqlStrings.Read("// var sql = \"UPDATE {table} SET hidden = @Value\";\nvar c = 'x';").ShouldBeEmpty();

    [Fact]
    public void IncludeBothConditionalConfigurations()
    {
        var strings = CSharpSqlStrings.Read("#if PROVIDER\nvar sql = \"first\";\n#else\nvar sql = \"second\";\n#endif");
        strings.Order(StringComparer.Ordinal).ShouldBe(["first", "second"]);
    }

    [Fact]
    public void RefuseMalformedSource() => Should.Throw<InvalidOperationException>(() => CSharpSqlStrings.Read("var sql = \"unterminated;"));

    [Theory]
    [InlineData("(\", missing=@Missing\")")]
    [InlineData("suffix")]
    public void PreserveConcatenatedColumns(string suffix) => CSharpSqlStrings.Read(
        "const string suffix = \", missing=@Missing\"; var sql = $\"UPDATE {table}\\nSET a=@A, b=@B, c=@C\" + " + suffix + ";")
        .ShouldContain("UPDATE {table}\nSET a=@A, b=@B, c=@C, missing=@Missing");

    [Fact]
    public void RefuseUnresolvedSqlComposition() => Should.Throw<InvalidOperationException>(() =>
        CSharpSqlStrings.Read("var sql = $\"UPDATE {table} SET a=@A, b=@B, c=@C\" + GetColumns();"));

    [Fact]
    public void PermitDiagnosticTextMentioningSql() => CSharpSqlStrings.Read("var message = \"Could not UPDATE record \" + id;")
        .ShouldContain("Could not UPDATE record ");

    [Fact]
    public void RefuseRecordDelimiterCollision() => Should.Throw<InvalidOperationException>(() =>
        CSharpSqlStrings.Read("var sql = \"UPDATE {table} SET a=@A\\n__END_CSHARP_SQL__\\n, missing=@Missing\";"));
}
