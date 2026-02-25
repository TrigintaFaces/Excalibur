// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Excalibur.Saga.Telemetry;

/// <summary>
/// Provides OpenTelemetry-compatible metrics for saga operations using System.Diagnostics.Metrics.
/// </summary>
/// <remarks>
/// <para>
/// This follows .NET best practices by using built-in APIs that OpenTelemetry can collect from,
/// without requiring a direct dependency on OpenTelemetry.
/// </para>
/// <para>
/// Metric names follow the OpenTelemetry semantic conventions with the <c>dispatch.saga</c> prefix:
/// <list type="bullet">
///   <item><c>dispatch.saga.started_total</c> - Counter of sagas started</item>
///   <item><c>dispatch.saga.completed_total</c> - Counter of sagas completed successfully</item>
///   <item><c>dispatch.saga.failed_total</c> - Counter of sagas failed</item>
///   <item><c>dispatch.saga.compensated_total</c> - Counter of sagas compensated</item>
///   <item><c>dispatch.saga.duration_ms</c> - Histogram of saga duration in milliseconds</item>
///   <item><c>dispatch.saga.handler_duration_ms</c> - Histogram of handler duration in milliseconds</item>
///   <item><c>dispatch.saga.active</c> - Gauge of currently active sagas</item>
/// </list>
/// </para>
/// <para>
/// All metrics include a <c>saga_type</c> tag with bounded cardinality (max 128 distinct values).
/// Tag lists are cached per saga type to avoid per-call allocation overhead.
/// </para>
/// </remarks>
public static class SagaMetrics
{
	/// <summary>
	/// The meter name for saga metrics.
	/// </summary>
	public const string MeterName = "Excalibur.Dispatch.Sagas";

	/// <summary>
	/// The meter version.
	/// </summary>
	public const string MeterVersion = "1.0.0";

	/// <summary>
	/// Maximum number of distinct saga type tag values before overflow.
	/// </summary>
	private const int MaxSagaTypeCardinality = 128;

	/// <summary>
	/// Overflow sentinel for saga types that exceed the cardinality limit.
	/// </summary>
	private const string OverflowSagaType = "__other__";

	private static readonly Meter Meter = new(MeterName, MeterVersion);

	/// <summary>
	/// Counter for sagas started.
	/// </summary>
	private static readonly Counter<long> SagaStartedCounter = Meter.CreateCounter<long>(
		"dispatch.saga.started_total",
		unit: "sagas",
		description: "Total number of sagas started");

	/// <summary>
	/// Counter for sagas completed successfully.
	/// </summary>
	private static readonly Counter<long> SagaCompletedCounter = Meter.CreateCounter<long>(
		"dispatch.saga.completed_total",
		unit: "sagas",
		description: "Total number of sagas completed successfully");

	/// <summary>
	/// Counter for sagas failed.
	/// </summary>
	private static readonly Counter<long> SagaFailedCounter = Meter.CreateCounter<long>(
		"dispatch.saga.failed_total",
		unit: "sagas",
		description: "Total number of sagas failed");

	/// <summary>
	/// Counter for sagas compensated.
	/// </summary>
	private static readonly Counter<long> SagaCompensatedCounter = Meter.CreateCounter<long>(
		"dispatch.saga.compensated_total",
		unit: "sagas",
		description: "Total number of sagas compensated");

	/// <summary>
	/// Histogram for saga duration.
	/// </summary>
	private static readonly Histogram<double> SagaDurationHistogram = Meter.CreateHistogram<double>(
		"dispatch.saga.duration_ms",
		unit: "milliseconds",
		description: "Duration of saga execution from start to completion");

	/// <summary>
	/// Histogram for handler duration.
	/// </summary>
	private static readonly Histogram<double> SagaHandlerDurationHistogram = Meter.CreateHistogram<double>(
		"dispatch.saga.handler_duration_ms",
		unit: "milliseconds",
		description: "Duration of saga handler execution");

	/// <summary>
	/// Dictionary tracking active saga counts by saga type.
	/// </summary>
	private static readonly ConcurrentDictionary<string, long> ActiveSagaCounts = new();

	/// <summary>
	/// Cached tag lists per saga type to avoid per-call allocation.
	/// Bounded to <see cref="MaxSagaTypeCardinality"/> distinct values.
	/// </summary>
	private static readonly ConcurrentDictionary<string, KeyValuePair<string, object?>[]> CachedTagLists = new();

	/// <summary>
	/// Tracks known saga type values for cardinality bounding.
	/// </summary>
	private static readonly ConcurrentDictionary<string, byte> KnownSagaTypes = new(
		concurrencyLevel: Environment.ProcessorCount,
		capacity: MaxSagaTypeCardinality);

	static SagaMetrics()
	{
		// Create observable gauge for active sagas
		_ = Meter.CreateObservableGauge(
			"dispatch.saga.active",
			observeValues: static () =>
			{
				return ActiveSagaCounts.Select(static kvp => new Measurement<long>(
					kvp.Value,
					new KeyValuePair<string, object?>("saga_type", kvp.Key)));
			},
			unit: "sagas",
			description: "Number of currently active sagas");
	}

	/// <summary>
	/// Records that a saga was started.
	/// </summary>
	/// <param name="sagaType">The short type name of the saga (not assembly-qualified).</param>
	public static void RecordSagaStarted(string sagaType)
	{
		SagaStartedCounter.Add(1, GetOrCreateTags(sagaType));
	}

	/// <summary>
	/// Records that a saga was completed successfully.
	/// </summary>
	/// <param name="sagaType">The short type name of the saga (not assembly-qualified).</param>
	public static void RecordSagaCompleted(string sagaType)
	{
		SagaCompletedCounter.Add(1, GetOrCreateTags(sagaType));
	}

	/// <summary>
	/// Records that a saga failed.
	/// </summary>
	/// <param name="sagaType">The short type name of the saga (not assembly-qualified).</param>
	public static void RecordSagaFailed(string sagaType)
	{
		SagaFailedCounter.Add(1, GetOrCreateTags(sagaType));
	}

	/// <summary>
	/// Records that a saga was compensated.
	/// </summary>
	/// <param name="sagaType">The short type name of the saga (not assembly-qualified).</param>
	public static void RecordSagaCompensated(string sagaType)
	{
		SagaCompensatedCounter.Add(1, GetOrCreateTags(sagaType));
	}

	/// <summary>
	/// Records the duration of a saga from start to completion.
	/// </summary>
	/// <param name="sagaType">The short type name of the saga (not assembly-qualified).</param>
	/// <param name="durationMs">Duration in milliseconds.</param>
	public static void RecordSagaDuration(string sagaType, double durationMs)
	{
		SagaDurationHistogram.Record(durationMs, GetOrCreateTags(sagaType));
	}

	/// <summary>
	/// Records the duration of a saga handler execution.
	/// </summary>
	/// <param name="sagaType">The short type name of the saga (not assembly-qualified).</param>
	/// <param name="durationMs">Duration in milliseconds.</param>
	public static void RecordHandlerDuration(string sagaType, double durationMs)
	{
		SagaHandlerDurationHistogram.Record(durationMs, GetOrCreateTags(sagaType));
	}

	/// <summary>
	/// Increments the active saga count for the specified saga type.
	/// Call when a saga starts processing.
	/// </summary>
	/// <param name="sagaType">The short type name of the saga (not assembly-qualified).</param>
	public static void IncrementActive(string sagaType) =>
		ActiveSagaCounts.AddOrUpdate(GuardSagaType(sagaType), 1, static (_, v) => v + 1);

	/// <summary>
	/// Decrements the active saga count for the specified saga type.
	/// Call when a saga completes, fails, or is compensated.
	/// </summary>
	/// <param name="sagaType">The short type name of the saga (not assembly-qualified).</param>
	public static void DecrementActive(string sagaType) =>
		ActiveSagaCounts.AddOrUpdate(GuardSagaType(sagaType), 0, static (_, v) => Math.Max(0, v - 1));

	/// <summary>
	/// Gets the current active saga count for the specified saga type.
	/// Primarily used for testing.
	/// </summary>
	/// <param name="sagaType">The short type name of the saga (not assembly-qualified).</param>
	/// <returns>The current active saga count, or 0 if not tracked.</returns>
	public static long GetActiveCount(string sagaType) =>
		ActiveSagaCounts.GetValueOrDefault(sagaType, 0);

	/// <summary>
	/// Resets all active saga counts to zero.
	/// Primarily used for testing.
	/// </summary>
	public static void ResetActiveCounts() =>
		ActiveSagaCounts.Clear();

	/// <summary>
	/// Guards the saga type string against unbounded cardinality.
	/// Returns the original value if within the limit, otherwise returns overflow sentinel.
	/// </summary>
	private static string GuardSagaType(string? sagaType)
	{
		if (sagaType is null)
		{
			return OverflowSagaType;
		}

		// Fast path: already tracked
		if (KnownSagaTypes.ContainsKey(sagaType))
		{
			return sagaType;
		}

		// Check cardinality limit before adding
		if (KnownSagaTypes.Count >= MaxSagaTypeCardinality)
		{
			return OverflowSagaType;
		}

		KnownSagaTypes.TryAdd(sagaType, 0);
		return sagaType;
	}

	/// <summary>
	/// Gets or creates a cached tag list for the given saga type.
	/// Avoids per-call allocation of tag arrays.
	/// </summary>
	private static KeyValuePair<string, object?>[] GetOrCreateTags(string sagaType)
	{
		var guardedType = GuardSagaType(sagaType);
		return CachedTagLists.GetOrAdd(guardedType, static key =>
		[
			new KeyValuePair<string, object?>("saga_type", key),
		]);
	}
}
