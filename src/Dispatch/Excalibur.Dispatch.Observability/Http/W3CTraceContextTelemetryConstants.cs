// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Observability.Http;

/// <summary>
/// Telemetry constants for the W3C Trace Context propagation middleware.
/// </summary>
public static class W3CTraceContextTelemetryConstants
{
	/// <summary>
	/// The ActivitySource name for W3C trace context propagation.
	/// </summary>
	public const string ActivitySourceName = "Excalibur.Dispatch.Observability.W3CTraceContext";

	/// <summary>
	/// The version for ActivitySource instances, derived from the assembly informational version.
	/// </summary>
	public static readonly string Version =
		typeof(W3CTraceContextTelemetryConstants).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
