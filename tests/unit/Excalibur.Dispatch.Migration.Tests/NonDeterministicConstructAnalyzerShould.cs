// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Migration.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace Excalibur.Dispatch.Migration.Tests;

/// <summary>
/// bd-4m51xs — lock for AC-10 (FR-14): a MediatR construct with no deterministic mechanical
/// rewrite (pre/post-processor, exception handler/action, stream pipeline behavior) surfaces
/// an informational EXMIG0002 diagnostic — never a silent skip. Non-vacuous: the verifier
/// asserts the exact diagnostic at the offending base-type span.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compat")]
public sealed class NonDeterministicConstructAnalyzerShould
{
    private const string MediatRStubs = """
        namespace MediatR
        {
            public interface IRequestPreProcessor<in TRequest> { }
            public interface IRequestPostProcessor<in TRequest, in TResponse> { }
            public interface IRequestHandler<in TRequest, TResponse> { }
        }
        """;

    [Fact] // AC-10: a non-portable construct (IRequestPreProcessor) is flagged, not silently skipped.
    public async Task ReportEXMIG0002_ForANonPortableConstruct()
    {
        const string source = """
            using MediatR;

            public sealed class LoggingPreProcessor : {|#0:IRequestPreProcessor<string>|}
            {
            }
            """;

        var test = new CSharpAnalyzerTest<NonDeterministicConstructAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { MediatRStubs, source } },
        };
        // EXMIG0002 = Info; args = (declaring type, construct name).
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("EXMIG0002", DiagnosticSeverity.Info)
                .WithLocation(0)
                .WithArguments("LoggingPreProcessor", "IRequestPreProcessor"));

        await test.RunAsync();
    }

    [Fact] // No false positive on a portable construct (IRequestHandler is mechanically shimmed).
    public async Task NotReport_ForAPortableHandler()
    {
        const string source = """
            using MediatR;

            public sealed class PingHandler : IRequestHandler<string, int>
            {
            }
            """;

        var test = new CSharpAnalyzerTest<NonDeterministicConstructAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { MediatRStubs, source } },
        };

        await test.RunAsync();
    }
}
