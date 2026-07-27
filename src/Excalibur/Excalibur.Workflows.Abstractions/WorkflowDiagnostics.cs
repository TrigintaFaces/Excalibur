// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Workflows;

/// <summary>
/// Diagnostic identifiers for the durable workflow engine's distributed tracing.
/// </summary>
/// <remarks>
/// The engine emits <see cref="System.Diagnostics.Activity"/> spans for workflow execution, activity
/// invocation, and durable-timer firing from an <see cref="System.Diagnostics.ActivitySource"/> named
/// <see cref="ActivitySourceName"/>. Subscribe an <see cref="System.Diagnostics.ActivityListener"/> or an
/// OpenTelemetry tracer provider to that source name to collect them.
/// </remarks>
public static class WorkflowDiagnostics
{
	/// <summary>
	/// The name of the <see cref="System.Diagnostics.ActivitySource"/> the workflow engine emits spans from.
	/// </summary>
	public const string ActivitySourceName = "Excalibur.Workflows";
}
