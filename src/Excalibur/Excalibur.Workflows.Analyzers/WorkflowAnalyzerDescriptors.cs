// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis;

namespace Excalibur.Workflows.Analyzers;

/// <summary>
/// Central diagnostic descriptors for the workflow determinism analyzers.
/// </summary>
internal static class WorkflowAnalyzerDescriptors
{
    private const string DeterminismCategory = "Excalibur.Workflows.Determinism";

    /// <summary>
    /// EXWF001: a non-deterministic API is invoked inside a durable workflow body.
    /// </summary>
    /// <remarks>
    /// Reported as an error, not a warning: a workflow body that diverges on replay silently corrupts durable
    /// state — the engine re-executes it against a journal recorded by a different decision sequence. The
    /// failure surfaces long after the offending line, so it is caught at compile time rather than deferred.
    /// </remarks>
    public static readonly DiagnosticDescriptor NonDeterministicApiInWorkflow = new(
        id: WorkflowDiagnosticIds.NonDeterministicApiInWorkflow,
        title: "Non-deterministic API used inside a workflow body",
        messageFormat: "'{0}' is non-deterministic and breaks durable replay inside a workflow body; use '{1}' instead",
        category: DeterminismCategory,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A durable workflow body must be deterministic: on replay the engine re-executes the body "
            + "and expects the same sequence of decisions. Reading the ambient clock (DateTime.Now/UtcNow, "
            + "DateTimeOffset.Now/UtcNow), generating identifiers (Guid.NewGuid, Guid.CreateVersion7) or random "
            + "values (Random, RandomNumberGenerator), delaying on the wall clock (Task.Delay, Thread.Sleep), and "
            + "reading elapsed-time counters (Environment.TickCount, Stopwatch) all produce different values each "
            + "run and diverge on replay. Obtain a clock reading or identifier through the deterministic "
            + "IWorkflowContext primitives (ctx.UtcNowAsync / ctx.NewGuidAsync), which journal the value on first "
            + "execution and replay it thereafter; express a delay as ctx.CreateTimerAsync; and compute a random "
            + "value inside an activity (ctx.CallActivityAsync), whose result the engine journals and replays.",
        helpLinkUri: "https://docs.excalibur-dispatch.dev/docs/diagnostics/EXWF001");
}
