// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

namespace Excalibur.Workflows;

/// <summary>
/// The process-wide <see cref="ActivitySource"/> the workflow engine emits tracing spans from, and the
/// tag names used on those spans. Built on <see cref="ActivitySource"/> so consumers collect spans with a
/// standard <see cref="ActivityListener"/> or OpenTelemetry, without a bespoke tracing abstraction.
/// </summary>
internal static class WorkflowActivitySource
{
    /// <summary>The shared activity source, named for consumer subscription (see <see cref="WorkflowDiagnostics"/>).</summary>
    internal static readonly ActivitySource Source = new(WorkflowDiagnostics.ActivitySourceName);

    internal const string ExecuteSpanName = "workflow.execute";
    internal const string ActivitySpanName = "workflow.activity";
    internal const string TimerSpanName = "workflow.timer";

    internal const string WorkflowNameTag = "workflow.name";
    internal const string InstanceIdTag = "workflow.instance_id";
    internal const string VersionTag = "workflow.version";
    internal const string ActivityNameTag = "workflow.activity_name";
    internal const string StepOrdinalTag = "workflow.step_ordinal";
}
