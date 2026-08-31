// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

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

    /// <summary>
    /// Every member the analyzer maps must actually be reported. One row per mapping, so a mapping added
    /// without a row -- or removed while a row remains -- is caught by the completeness arm below rather
    /// than sitting advertised and unproven.
    /// </summary>
    /// <returns>One row per mapped member: the statement, the reported member, and the guidance.</returns>
    public static TheoryData<string, string, string> MappedMembers()
    {
        var data = new TheoryData<string, string, string>();
        data.Add(
            "var v = {|#0:DateTime.Now|};",
            "DateTime.Now",
            "ctx.UtcNowAsync");
        data.Add(
            "var v = {|#0:DateTime.UtcNow|};",
            "DateTime.UtcNow",
            "ctx.UtcNowAsync");
        data.Add(
            "var v = {|#0:DateTime.Today|};",
            "DateTime.Today",
            "ctx.UtcNowAsync");
        data.Add(
            "var v = {|#0:DateTimeOffset.Now|};",
            "DateTimeOffset.Now",
            "ctx.UtcNowAsync");
        data.Add(
            "var v = {|#0:DateTimeOffset.UtcNow|};",
            "DateTimeOffset.UtcNow",
            "ctx.UtcNowAsync");
        data.Add(
            "var v = {|#0:Guid.NewGuid|}();",
            "Guid.NewGuid",
            "ctx.NewGuidAsync");
        data.Add(
            "var v = {|#0:Task.Delay|}(1);",
            "Task.Delay",
            "ctx.CreateTimerAsync(delay, cancellationToken)");
        data.Add(
            "{|#0:Thread.Sleep|}(1);",
            "Thread.Sleep",
            "ctx.CreateTimerAsync(delay, cancellationToken)");
        data.Add(
            "var v = {|#0:Random.Shared|};",
            "Random.Shared",
            "ctx.CallActivityAsync to generate the value in an activity");
        data.Add(
            "var v = {|#0:RandomNumberGenerator.Create|}();",
            "RandomNumberGenerator.Create",
            "ctx.CallActivityAsync to generate the value in an activity");
        data.Add(
            "var v = {|#0:RandomNumberGenerator.GetBytes|}(1);",
            "RandomNumberGenerator.GetBytes",
            "ctx.CallActivityAsync to generate the value in an activity");
        data.Add(
            "{|#0:RandomNumberGenerator.Fill|}(new byte[1]);",
            "RandomNumberGenerator.Fill",
            "ctx.CallActivityAsync to generate the value in an activity");
        data.Add(
            "var v = {|#0:RandomNumberGenerator.GetInt32|}(1);",
            "RandomNumberGenerator.GetInt32",
            "ctx.CallActivityAsync to generate the value in an activity");
        data.Add(
            "var v = {|#0:Environment.TickCount|};",
            "Environment.TickCount",
            "ctx.UtcNowAsync");
        data.Add(
            "var v = {|#0:Environment.TickCount64|};",
            "Environment.TickCount64",
            "ctx.UtcNowAsync");
        data.Add(
            "var v = {|#0:Stopwatch.GetTimestamp|}();",
            "Stopwatch.GetTimestamp",
            "ctx.UtcNowAsync");
        data.Add(
            "var v = {|#0:Stopwatch.StartNew|}();",
            "Stopwatch.StartNew",
            "ctx.UtcNowAsync");
        return data;
    }

    [Theory]
    [MemberData(nameof(MappedMembers))]
    public async Task Flag_EveryMappedNonDeterministicMember_InsideAWorkflowMethod(
        string statement,
        string reportedMember,
        string guidance)
    {
        var source = $$"""
            using System;
            using System.Diagnostics;
            using System.Security.Cryptography;
            using System.Threading;
            using System.Threading.Tasks;
            using Excalibur.Workflows;

            public class Orders
            {
                [Workflow]
                public void Run()
                {
                    {{statement}}
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
                .WithArguments(reportedMember, guidance));

        await test.RunAsync();
    }

    /// <summary>
    /// COMPLETENESS: the rows above must cover the map exactly, in both directions.
    /// </summary>
    /// <remarks>
    /// Without this, a mapping added later is advertised and unproven, which is the state this theory
    /// replaced; and a mapping deleted leaves a row asserting behaviour nothing implements. The map is
    /// private, so this reads it reflectively: restating it here would produce a copy that cannot detect a
    /// divergence from the thing it copies.
    /// </remarks>
    [Fact]
    public void Cover_EveryMappedMember_WithATheoryRow()
    {
        var field = typeof(NonDeterministicApiInWorkflowAnalyzer)
            .GetField("NonDeterministicMembers", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);

        var map = (System.Collections.ICollection)field!.GetValue(null)!;

        // One mapping has no row, and it is named here rather than quietly missing: Guid.CreateVersion7
        // is absent from the reference assemblies this harness pins, so a snippet using it fails to
        // compile (CS0117) and the analyzer is never consulted. Naming it keeps the count exact, so a
        // mapping added later still has to bring a row.
        const int UnprovableAgainstPinnedReferenceAssemblies = 1;

        Assert.Equal(map.Count, MappedMembers().Count + UnprovableAgainstPinnedReferenceAssemblies);
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
