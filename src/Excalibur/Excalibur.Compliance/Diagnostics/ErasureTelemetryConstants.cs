// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace Excalibur.Compliance.Diagnostics;

/// <summary>
/// Shared telemetry constants for GDPR erasure instrumentation.
/// </summary>
internal static class ErasureTelemetryConstants
{
	/// <summary>
	/// The meter name for erasure metrics.
	/// </summary>
	public const string MeterName = "Excalibur.Compliance.Erasure";

	/// <summary>
	/// The activity source name for erasure distributed tracing.
	/// </summary>
	public const string ActivitySourceName = "Excalibur.Compliance.Erasure";

	/// <summary>
	/// Shared <see cref="ActivitySource"/> for erasure tracing.
	/// </summary>
	public static ActivitySource ActivitySource { get; } = new(ActivitySourceName);

	/// <summary>
	/// Shared <see cref="Meter"/> for erasure metrics.
	/// </summary>
	public static Meter Meter { get; } = new(MeterName);

	/// <summary>
	/// Metric instrument names following OTel semantic conventions.
	/// </summary>
	[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants")]
	public static class MetricNames
	{
		/// <summary>Total erasure requests submitted.</summary>
		public const string RequestsSubmitted = "dispatch.erasure.requests.submitted";

		/// <summary>Total erasure requests completed.</summary>
		public const string RequestsCompleted = "dispatch.erasure.requests.completed";

		/// <summary>Total erasure request failures.</summary>
		public const string RequestsFailed = "dispatch.erasure.requests.failed";

		/// <summary>Total erasure requests blocked by legal hold.</summary>
		public const string RequestsBlocked = "dispatch.erasure.requests.blocked";

		/// <summary>Total keys deleted via erasure.</summary>
		public const string KeysDeleted = "dispatch.erasure.keys.deleted";

		/// <summary>Duration of erasure execution in milliseconds.</summary>
		public const string ExecutionDuration = "dispatch.erasure.execution.duration";

		/// <summary>
		/// What the legal-hold expiration sweep did to each expired hold it considered, tagged by
		/// <c>outcome</c>.
		/// </summary>
		/// <remarks>
		/// <para>
		/// One counter with an <c>outcome</c> dimension rather than a counter per outcome, so the outcomes
		/// are comparable in one query: a rising <c>contended</c> share against a flat <c>released</c> is
		/// the shape worth alerting on, and it is not visible if the two are separate series.
		/// </para>
		/// <para>
		/// <b>This exists because the log lines are not enough.</b> A log line is read by somebody who is
		/// already looking, and the condition it reports — a hold whose release keeps losing a concurrency
		/// check — is exactly the condition nobody is looking at. Left unresolved, that hold stays active
		/// past its statutory period. A counter is what an operator can alert on without reading text.
		/// </para>
		/// </remarks>
		public const string LegalHoldExpirationOutcomes = "dispatch.legal_hold.expiration.outcomes";
	}

	/// <summary>
	/// Tag names for erasure metrics.
	/// </summary>
	[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants")]
	public static class Tags
	{
		/// <summary>The erasure scope (full, tenant, selective).</summary>
		public const string Scope = "erasure.scope";

		/// <summary>The erasure result status (scheduled, blocked, failed).</summary>
		public const string ResultStatus = "erasure.result_status";

		/// <summary>The error type for failed operations.</summary>
		public const string ErrorType = "error.type";
	}
}
