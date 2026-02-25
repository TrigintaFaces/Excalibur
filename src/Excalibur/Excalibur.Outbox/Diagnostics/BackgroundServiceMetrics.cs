// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

namespace Excalibur.Outbox.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for background processing services (Outbox, Inbox).
/// </summary>
/// <remarks>
/// <para>
/// This class provides static counters and histograms for monitoring background
/// processor performance. Metrics follow OpenTelemetry naming conventions and
/// are registered under the <c>Excalibur.Dispatch.BackgroundServices</c> meter.
/// </para>
/// <para>
/// To enable collection, register the meter with your OpenTelemetry provider:
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .WithMetrics(m => m.AddMeter(BackgroundServiceMetrics.MeterName));
/// </code>
/// </para>
/// </remarks>
public static class BackgroundServiceMetrics
{
	/// <summary>
	/// The meter name for background service metrics.
	/// </summary>
	public const string MeterName = "Excalibur.Dispatch.BackgroundServices";

	/// <summary>
	/// The meter version.
	/// </summary>
	public const string MeterVersion = "1.0.0";

	private static readonly Meter Meter = new(MeterName, MeterVersion);

	private static readonly Counter<long> MessagesProcessedCounter =
		Meter.CreateCounter<long>(
			"excalibur.background_service.messages_processed",
			"messages",
			"Total number of messages processed by background services.");

	private static readonly Counter<long> MessagesFailedCounter =
		Meter.CreateCounter<long>(
			"excalibur.background_service.messages_failed",
			"messages",
			"Total number of messages that failed processing.");

	private static readonly Histogram<double> ProcessingDurationHistogram =
		Meter.CreateHistogram<double>(
			"excalibur.background_service.processing_duration",
			"ms",
			"Duration of processing cycles in milliseconds.");

	private static readonly Counter<long> ProcessingCyclesCounter =
		Meter.CreateCounter<long>(
			"excalibur.background_service.processing_cycles",
			"cycles",
			"Total number of processing cycles executed.");

	private static readonly Counter<long> ProcessingErrorsCounter =
		Meter.CreateCounter<long>(
			"excalibur.background_service.processing_errors",
			"errors",
			"Total number of processing cycle errors.");

	/// <summary>
	/// Records successfully processed messages.
	/// </summary>
	/// <param name="serviceType">The service type (e.g., "outbox", "inbox").</param>
	/// <param name="operation">The operation type (e.g., "pending", "scheduled", "retry").</param>
	/// <param name="count">The number of messages processed.</param>
	public static void RecordMessagesProcessed(string serviceType, string operation, long count)
	{
		if (count <= 0)
		{
			return;
		}

		MessagesProcessedCounter.Add(count,
			new KeyValuePair<string, object?>("service.type", serviceType),
			new KeyValuePair<string, object?>("operation", operation));
	}

	/// <summary>
	/// Records failed messages.
	/// </summary>
	/// <param name="serviceType">The service type (e.g., "outbox", "inbox").</param>
	/// <param name="operation">The operation type (e.g., "pending", "scheduled", "retry").</param>
	/// <param name="count">The number of messages that failed.</param>
	public static void RecordMessagesFailed(string serviceType, string operation, long count)
	{
		if (count <= 0)
		{
			return;
		}

		MessagesFailedCounter.Add(count,
			new KeyValuePair<string, object?>("service.type", serviceType),
			new KeyValuePair<string, object?>("operation", operation));
	}

	/// <summary>
	/// Records the duration of a processing cycle.
	/// </summary>
	/// <param name="serviceType">The service type (e.g., "outbox", "inbox").</param>
	/// <param name="durationMs">The duration in milliseconds.</param>
	public static void RecordProcessingDuration(string serviceType, double durationMs)
	{
		ProcessingDurationHistogram.Record(durationMs,
			new KeyValuePair<string, object?>("service.type", serviceType));
	}

	/// <summary>
	/// Records a completed processing cycle.
	/// </summary>
	/// <param name="serviceType">The service type (e.g., "outbox", "inbox").</param>
	/// <param name="result">The cycle result (e.g., "success", "partial", "empty").</param>
	public static void RecordProcessingCycle(string serviceType, string result)
	{
		ProcessingCyclesCounter.Add(1,
			new KeyValuePair<string, object?>("service.type", serviceType),
			new KeyValuePair<string, object?>("result", result));
	}

	/// <summary>
	/// Records a processing cycle error.
	/// </summary>
	/// <param name="serviceType">The service type (e.g., "outbox", "inbox").</param>
	/// <param name="errorType">The error type name.</param>
	public static void RecordProcessingError(string serviceType, string errorType)
	{
		ProcessingErrorsCounter.Add(1,
			new KeyValuePair<string, object?>("service.type", serviceType),
			new KeyValuePair<string, object?>("error.type", errorType));
	}
}

/// <summary>
/// Well-known service type constants for background service metrics.
/// </summary>
public static class BackgroundServiceTypes
{
	/// <summary>Outbox background service.</summary>
	public const string Outbox = "outbox";

	/// <summary>Inbox background service.</summary>
	public const string Inbox = "inbox";

	/// <summary>CDC (Change Data Capture) processor.</summary>
	public const string Cdc = "cdc";
}

/// <summary>
/// Well-known operation type constants for background service metrics.
/// </summary>
public static class BackgroundServiceOperations
{
	/// <summary>Processing pending messages.</summary>
	public const string Pending = "pending";

	/// <summary>Processing scheduled messages.</summary>
	public const string Scheduled = "scheduled";

	/// <summary>Retrying failed messages.</summary>
	public const string Retry = "retry";

	/// <summary>Dispatching inbox messages.</summary>
	public const string Dispatch = "dispatch";
}

/// <summary>
/// Well-known result constants for background service metrics.
/// </summary>
public static class BackgroundServiceResults
{
	/// <summary>All messages processed successfully.</summary>
	public const string Success = "success";

	/// <summary>Some messages failed.</summary>
	public const string Partial = "partial";

	/// <summary>No messages to process.</summary>
	public const string Empty = "empty";

	/// <summary>Processing cycle failed.</summary>
	public const string Error = "error";
}
