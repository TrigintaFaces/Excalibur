// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Migration.Analyzers;
using Excalibur.Dispatch.Migration.CodeFixes;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace Excalibur.Dispatch.Migration.Tests;

/// <summary>
/// bd-30qbi8 — lock for AC-15 (FR-12) + EC-8: the EXMIG0003 code-fix swaps
/// <c>using MediatR;</c> → <c>using Excalibur.Dispatch.Compat.MediatR;</c>, and when the compat
/// namespace is already imported it REMOVES the redundant directive instead of producing a
/// duplicate/orphan (idempotent). Non-vacuous: asserts the diagnostic span AND exact fixed source.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compat")]
public sealed class MediatRUsingDirectiveCodeFixShould
{
    // Empty namespace stubs so both pre- and post-fix using directives resolve (CS8019 unnecessary-using
    // is a hidden diagnostic, ignored by the verifier).
    private const string NamespaceStubs = """
        namespace MediatR { }
        namespace Excalibur.Dispatch.Compat.MediatR { }
        """;

    [Fact] // AC-15: using MediatR; -> using Excalibur.Dispatch.Compat.MediatR;
    public async Task SwapMediatRUsingToCompatNamespace()
    {
        const string source = """
            {|#0:using MediatR;|}
            """;

        const string fixedSource = """
            using Excalibur.Dispatch.Compat.MediatR;
            """;

        var test = new CSharpCodeFixTest<MediatRUsingDirectiveAnalyzer, MediatRUsingDirectiveCodeFixProvider, DefaultVerifier>
        {
            TestState = { Sources = { NamespaceStubs, source } },
            FixedState = { Sources = { NamespaceStubs, fixedSource } },
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("EXMIG0003", DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("MediatR"));

        await test.RunAsync();
    }

    [Fact] // EC-8: compat already imported -> drop the redundant 'using MediatR;' (no duplicate).
    public async Task RemoveRedundantMediatRUsing_WhenCompatAlreadyImported()
    {
        const string source = """
            {|#0:using MediatR;|}
            using Excalibur.Dispatch.Compat.MediatR;
            """;

        const string fixedSource = """
            using Excalibur.Dispatch.Compat.MediatR;
            """;

        var test = new CSharpCodeFixTest<MediatRUsingDirectiveAnalyzer, MediatRUsingDirectiveCodeFixProvider, DefaultVerifier>
        {
            TestState = { Sources = { NamespaceStubs, source } },
            FixedState = { Sources = { NamespaceStubs, fixedSource } },
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("EXMIG0003", DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("MediatR"));

        await test.RunAsync();
    }
}
