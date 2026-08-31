// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Constants for Claim Check OpenTelemetry telemetry names and semantic conventions.
/// </summary>
internal static class ClaimCheckTelemetryConstants
{
	/// <summary>
	/// The meter name for claim check telemetry.
	/// </summary>
	public const string MeterName = "Excalibur.Dispatch.Patterns.ClaimCheck";

	/// <summary>
	/// The activity source name for claim check telemetry.
	/// </summary>
	public const string ActivitySourceName = "Excalibur.Dispatch.Patterns.ClaimCheck";

	/// <summary>
	/// The process-lifetime meter that claim check instruments are created on. The name is fixed, so a
	/// single static instance is correct and lets a consumer subscribe by name once for the process.
	/// </summary>
	public static readonly Meter Meter = new(MeterName);

	/// <summary>
	/// The process-lifetime activity source claim check spans are started from.
	/// </summary>
	public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

	/// <summary>
	/// Standard metric names for claim check operations.
	/// </summary>
	[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants")]
	public static class MetricNames
	{
		/// <summary>
		/// Counter: total payloads stored successfully.
		/// </summary>
		public const string PayloadsStored = "dispatch.claimcheck.payloads.stored";

		/// <summary>
		/// Counter: total payloads retrieved successfully.
		/// </summary>
		public const string PayloadsRetrieved = "dispatch.claimcheck.payloads.retrieved";

		/// <summary>
		/// Counter: total payloads deleted successfully.
		/// </summary>
		public const string PayloadsDeleted = "dispatch.claimcheck.payloads.deleted";

		/// <summary>
		/// Counter: total operation failures.
		/// </summary>
		public const string OperationsFailed = "dispatch.claimcheck.operations.failed";

		/// <summary>
		/// Histogram: duration of store operations in milliseconds.
		/// </summary>
		public const string StoreDuration = "dispatch.claimcheck.store.duration";

		/// <summary>
		/// Histogram: duration of retrieve operations in milliseconds.
		/// </summary>
		public const string RetrieveDuration = "dispatch.claimcheck.retrieve.duration";

		/// <summary>
		/// Histogram: payload size in bytes.
		/// </summary>
		public const string PayloadSize = "dispatch.claimcheck.payload.size";
	}

	/// <summary>
	/// Standard tag names for claim check telemetry.
	/// </summary>
	[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants")]
	public static class Tags
	{
		/// <summary>
		/// The claim check operation (store, retrieve, delete).
		/// </summary>
		public const string Operation = "dispatch.claimcheck.operation";

		/// <summary>
		/// The error type for failed operations.
		/// </summary>
		public const string ErrorType = "error.type";
	}
}
