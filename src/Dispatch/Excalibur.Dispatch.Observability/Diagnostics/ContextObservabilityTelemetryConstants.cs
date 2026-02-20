// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Observability.Diagnostics;

/// <summary>
/// Telemetry constants for context observability components.
/// </summary>
public static class ContextObservabilityTelemetryConstants
{
	/// <summary>
	/// The meter name for context flow metrics.
	/// </summary>
	public const string MeterName = "Excalibur.Dispatch.Observability.Context";

	/// <summary>
	/// The ActivitySource name for context flow tracking.
	/// </summary>
	public const string ActivitySourceName = "Excalibur.Dispatch.Observability.Context";

	/// <summary>
	/// The version for ActivitySource instances, derived from the assembly informational version.
	/// </summary>
	public static readonly string Version =
		typeof(ContextObservabilityTelemetryConstants).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

	/// <summary>
	/// The ActivitySource name for context observability middleware.
	/// </summary>
	public const string MiddlewareActivitySourceName = "Excalibur.Dispatch.Observability.ContextMiddleware";
}
