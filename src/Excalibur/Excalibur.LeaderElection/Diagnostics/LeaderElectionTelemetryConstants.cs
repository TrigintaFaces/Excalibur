// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.LeaderElection.Diagnostics;

/// <summary>
/// Constants for leader election OpenTelemetry telemetry names and semantic conventions.
/// Follows the <c>Excalibur.LeaderElection</c> naming pattern matching the package namespace.
/// </summary>
public static class LeaderElectionTelemetryConstants
{
	/// <summary>
	/// The Meter name for leader election instrumentation.
	/// </summary>
	public const string MeterName = "Excalibur.LeaderElection";

	/// <summary>
	/// The ActivitySource name for leader election distributed tracing.
	/// </summary>
	public const string ActivitySourceName = "Excalibur.LeaderElection";

	/// <summary>
	/// Standard metric names for leader election operations.
	/// </summary>
	[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants")]
	public static class MetricNames
	{
		/// <summary>
		/// Counter: lease acquisition attempts.
		/// </summary>
		public const string Acquisitions = "excalibur.leaderelection.acquisitions";

		/// <summary>
		/// Histogram: duration leader holds lease in seconds.
		/// </summary>
		public const string LeaseDuration = "excalibur.leaderelection.lease_duration";

		/// <summary>
		/// ObservableGauge: current leadership status (0 or 1).
		/// </summary>
		public const string IsLeader = "excalibur.leaderelection.is_leader";
	}

	/// <summary>
	/// Standard tag names for leader election telemetry.
	/// </summary>
	[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants")]
	public static class Tags
	{
		/// <summary>
		/// The leader election instance identifier.
		/// </summary>
		public const string Instance = "excalibur.leaderelection.instance";

		/// <summary>
		/// The result of an acquisition attempt
		/// (acquired, lost, failed).
		/// </summary>
		public const string Result = "excalibur.leaderelection.result";

		/// <summary>
		/// The leader election provider name
		/// (sqlserver, redis, consul, kubernetes, inmemory).
		/// </summary>
		public const string Provider = "excalibur.leaderelection.provider";
	}
}
