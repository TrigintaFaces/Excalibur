// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Workflows.Analyzers;
using Excalibur.Workflows.CodeFixes;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace Excalibur.Workflows.Analyzers.Tests;

/// <summary>
/// Locks for the EXWF001 code-fix — a flagged non-deterministic API read is rewritten to the deterministic
/// <c>IWorkflowContext</c> primitive, discovering the context and cancellation-token parameters in scope.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Workflows")]
public sealed class UseWorkflowContextPrimitiveCodeFixShould
{
    // Full-enough workflow surface so BOTH the before (DateTime.UtcNow) and after (await ctx.UtcNowAsync)
    // sources compile.
    private const string WorkflowStubs = """
        namespace Excalibur.Workflows
        {
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
            public sealed class WorkflowAttribute : Attribute { }

            public interface IWorkflowContext
            {
                ValueTask<DateTimeOffset> UtcNowAsync(CancellationToken cancellationToken);
                ValueTask<Guid> NewGuidAsync(CancellationToken cancellationToken);
            }
        }
        """;

    [Fact]
    public async Task RewriteDateTimeUtcNow_ToAwaitCtxUtcNowAsync()
    {
        const string source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Excalibur.Workflows;

            public class Orders
            {
                [Workflow]
                public async Task Run(IWorkflowContext ctx, CancellationToken cancellationToken)
                {
                    var now = {|#0:DateTime.UtcNow|};
                    await Task.CompletedTask;
                }
            }
            """;

        const string fixedSource = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Excalibur.Workflows;

            public class Orders
            {
                [Workflow]
                public async Task Run(IWorkflowContext ctx, CancellationToken cancellationToken)
                {
                    var now = await ctx.UtcNowAsync(cancellationToken);
                    await Task.CompletedTask;
                }
            }
            """;

        var test = new CSharpCodeFixTest<
            NonDeterministicApiInWorkflowAnalyzer, UseWorkflowContextPrimitiveCodeFixProvider, DefaultVerifier>
        {
            ReferenceAssemblies = AnalyzerTestDefaults.ReferenceAssemblies,
            TestState = { Sources = { WorkflowStubs, source } },
            FixedState = { Sources = { WorkflowStubs, fixedSource } },
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(WorkflowDiagnosticIds.NonDeterministicApiInWorkflow, DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("DateTime.UtcNow", "ctx.UtcNowAsync"));

        await test.RunAsync();
    }

    [Fact]
    public async Task RewriteGuidNewGuid_ToAwaitCtxNewGuidAsync()
    {
        const string source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Excalibur.Workflows;

            public class Orders
            {
                [Workflow]
                public async Task Run(IWorkflowContext ctx, CancellationToken cancellationToken)
                {
                    var id = {|#0:Guid.NewGuid|}();
                    await Task.CompletedTask;
                }
            }
            """;

        const string fixedSource = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Excalibur.Workflows;

            public class Orders
            {
                [Workflow]
                public async Task Run(IWorkflowContext ctx, CancellationToken cancellationToken)
                {
                    var id = await ctx.NewGuidAsync(cancellationToken);
                    await Task.CompletedTask;
                }
            }
            """;

        var test = new CSharpCodeFixTest<
            NonDeterministicApiInWorkflowAnalyzer, UseWorkflowContextPrimitiveCodeFixProvider, DefaultVerifier>
        {
            ReferenceAssemblies = AnalyzerTestDefaults.ReferenceAssemblies,
            TestState = { Sources = { WorkflowStubs, source } },
            FixedState = { Sources = { WorkflowStubs, fixedSource } },
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(WorkflowDiagnosticIds.NonDeterministicApiInWorkflow, DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Guid.NewGuid", "ctx.NewGuidAsync"));

        await test.RunAsync();
    }
}
