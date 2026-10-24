// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using RepositoryChecks;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class DocumentationReferencesShould
{
    [Fact]
    public void RefuseAnEmptyConsumerPopulation() =>
        Should.Throw<InvalidOperationException>(() => DocumentationReferences.Create([]));

    [Theory]
    [InlineData("relative.dll")]
    [InlineData("")]
    public void RefuseUnresolvedReferencePaths(string path) =>
        Should.Throw<InvalidOperationException>(() => DocumentationReferences.Create([path]));

    [Fact]
    public void KeepUnresolvedPrivateImplementationApisUnavailable()
    {
        var references = DocumentationReferences.Create([typeof(object).Assembly.Location]);
        var allowed = new DocumentationBlock("consumer.md", 1, "compile", "public class Example { public object Value = new object(); }");
        var denied = allowed with { Code = "public class Example { public Boundary.Tests.DocumentationReferencesShould Value; }" };
        DocumentationApiRule.Analyze(allowed, [allowed], references).Selected
            .ShouldNotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        DocumentationApiRule.Analyze(denied, [denied], references).Selected
            .ShouldContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }
}
