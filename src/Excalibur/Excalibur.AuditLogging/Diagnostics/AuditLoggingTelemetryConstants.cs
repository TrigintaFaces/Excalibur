// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

namespace Excalibur.AuditLogging.Diagnostics;

/// <summary>
/// Shared telemetry constants for audit logging instrumentation.
/// </summary>
internal static class AuditLoggingTelemetryConstants
{
	/// <summary>
	/// The meter name for audit logging metrics.
	/// </summary>
	public const string MeterName = "Excalibur.AuditLogging";

	/// <summary>
	/// Shared <see cref="Meter"/> for audit logging metrics.
	/// </summary>
	public static Meter Meter { get; } = new(MeterName);

	/// <summary>
	/// Metric instrument names following OTel semantic conventions.
	/// </summary>
	public static class MetricNames
	{
		/// <summary>Total audit assertions dropped due to MaxAssertionsPerScope being exceeded.</summary>
		public const string AssertionsDropped = "dispatch.audit.assertions.dropped";
	}
}
