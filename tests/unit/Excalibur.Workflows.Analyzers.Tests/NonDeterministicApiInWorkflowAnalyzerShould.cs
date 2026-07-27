// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Workflows.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace Excalibur.Workflows.Analyzers.Tests;

/// <summary>
/// Locks for EXWF001 — the analyzer flags non-deterministic API usage inside a <c>[Workflow]</c> body and
/// stays silent outside one.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Workflows")]
public sealed class NonDeterministicApiInWorkflowAnalyzerShould
{
    // Minimal stubs for the workflow surface so the analyzed source compiles standalone; the analyzer
    // matches Excalibur.Workflows.WorkflowAttribute by full name and DateTime/Guid by BCL symbol.
    private const string WorkflowStubs = """
        namespace Excalibur.Workflows
        {
            using System;

            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
            public sealed class WorkflowAttribute : Attribute { }

            public interface IWorkflowContext { }
        }
        """;

    [Fact]
    public async Task Flag_DateTimeUtcNow_InsideAWorkflowMethod()
    {
        const string source = """
            using System;
            using Excalibur.Workflows;

            public class Orders
            {
                [Workflow]
                public void Run()
                {
                    var now = {|#0:DateTime.UtcNow|};
                }
            }
            """;

        var test = new CSharpAnalyzerTest<NonDeterministicApiInWorkflowAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = AnalyzerTestDefaults.ReferenceAssemblies,
            TestState = { Sources = { WorkflowStubs, source } },
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(WorkflowDiagnosticIds.NonDeterministicApiInWorkflow, DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("DateTime.UtcNow", "ctx.UtcNowAsync"));

        await test.RunAsync();
    }

    /// <summary>
    /// Binds the analyzer's <c>Random.Shared</c> rule. This lock only means anything because the harness
    /// pins modern reference assemblies: <c>Random.Shared</c> is .NET 6+, and against the harness default it
    /// does not exist, so the snippet fails with <c>CS0117</c> and the analyzer is never consulted.
    /// </summary>
    [Fact]
    public async Task Flag_RandomShared_InsideAWorkflowMethod()
    {
        const string source = """
            using System;
            using Excalibur.Workflows;

            public class Orders
            {
                [Workflow]
                public void Run()
                {
                    var n = {|#0:Random.Shared|}.Next();
                }
            }
            """;

        var test = new CSharpAnalyzerTest<NonDeterministicApiInWorkflowAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = AnalyzerTestDefaults.ReferenceAssemblies,
            TestState = { Sources = { WorkflowStubs, source } },
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(WorkflowDiagnosticIds.NonDeterministicApiInWorkflow, DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Random.Shared", "ctx.CallActivityAsync to generate the value in an activity"));

        await test.RunAsync();
    }

    [Fact]
    public async Task Flag_GuidNewGuid_InsideAWorkflowMethod()
    {
        const string source = """
            using System;
            using Excalibur.Workflows;

            public class Orders
            {
                [Workflow]
                public void Run()
                {
                    var id = {|#0:Guid.NewGuid|}();
                }
            }
            """;

        var test = new CSharpAnalyzerTest<NonDeterministicApiInWorkflowAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = AnalyzerTestDefaults.ReferenceAssemblies,
            TestState = { Sources = { WorkflowStubs, source } },
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(WorkflowDiagnosticIds.NonDeterministicApiInWorkflow, DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Guid.NewGuid", "ctx.NewGuidAsync"));

        await test.RunAsync();
    }

    [Fact]
    public async Task StaySilent_OutsideAWorkflowMethod()
    {
        // No [Workflow] attribute — the same call is ordinary code and must not be flagged.
        const string source = """
            using System;

            public class Ordinary
            {
                public void Run()
                {
                    var now = DateTime.UtcNow;
                    var id = Guid.NewGuid();
                }
            }
            """;

        var test = new CSharpAnalyzerTest<NonDeterministicApiInWorkflowAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = AnalyzerTestDefaults.ReferenceAssemblies,
            TestState = { Sources = { WorkflowStubs, source } },
        };

        await test.RunAsync();
    }
}
