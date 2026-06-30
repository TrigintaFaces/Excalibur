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
/// bd-hm6qlj — lock for AC-16 (FR-13): the EXMIG0004 handler-signature diagnostic on a legacy
/// <c>HandleAsync</c> method is fixed by the deterministic <c>HandleAsync</c>→<c>Handle</c> rename
/// (the only mechanically-safe transform per SA 17845/ADR-341 §5). Non-vacuous: the verifier
/// asserts the diagnostic span AND the exact fixed source.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compat")]
public sealed class HandlerSignatureCodeFixShould
{
    private const string MediatRStubs = """
        namespace MediatR
        {
            public interface IRequestHandler<in TRequest, TResponse> { }
        }
        """;

    [Fact] // AC-16: HandleAsync → Handle deterministic rename.
    public async Task RenameHandleAsyncToHandle()
    {
        const string source = """
            using MediatR;
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class PingHandler : IRequestHandler<string, int>
            {
                public Task<int> {|#0:HandleAsync|}(string request, CancellationToken cancellationToken)
                    => Task.FromResult(0);
            }
            """;

        const string fixedSource = """
            using MediatR;
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class PingHandler : IRequestHandler<string, int>
            {
                public Task<int> Handle(string request, CancellationToken cancellationToken)
                    => Task.FromResult(0);
            }
            """;

        var test = new CSharpCodeFixTest<HandlerSignatureAnalyzer, HandlerSignatureCodeFixProvider, DefaultVerifier>
        {
            TestState = { Sources = { MediatRStubs, source } },
            FixedState = { Sources = { MediatRStubs, fixedSource } },
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("EXMIG0004", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("PingHandler", "HandleAsync", "Handle"));

        await test.RunAsync();
    }
}
