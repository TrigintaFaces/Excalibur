// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;

namespace Excalibur.EventSourcing.Observability;

/// <summary>
/// Provides centralized activity source for Event Sourcing tracing operations.
/// </summary>
/// <remarks>
/// <para>
/// This class provides static helper methods for creating OpenTelemetry activity spans
/// for Event Sourcing operations. Activities are automatically linked to parent spans
/// when available.
/// </para>
/// <para>
/// When no listener is attached, all operations are no-ops with zero allocation overhead.
/// </para>
/// </remarks>
public static class EventSourcingActivitySource
{
	/// <summary>
	/// The activity source name for Event Sourcing tracing.
	/// </summary>
	public const string Name = "Excalibur.EventSourcing";

	/// <summary>
	/// The activity source instance for Event Sourcing operations.
	/// Process-lifetime singleton — do not dispose.
	/// </summary>
	public static ActivitySource Instance { get; } = new(Name);

	/// <summary>
	/// Starts an activity for an event store append operation.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="eventCount">The number of events being appended.</param>
	/// <param name="expectedVersion">The expected current version.</param>
	/// <returns>The started activity, or null if no listener is interested.</returns>
	public static Activity? StartAppendActivity(
		string aggregateId,
		string aggregateType,
		int eventCount,
		long expectedVersion)
	{
		var activity = Instance.StartActivity(EventSourcingActivities.Append);
		if (activity != null)
		{
			_ = activity.SetTag(EventSourcingTags.AggregateId, aggregateId);
			_ = activity.SetTag(EventSourcingTags.AggregateType, aggregateType);
			_ = activity.SetTag(EventSourcingTags.EventCount, eventCount);
			_ = activity.SetTag(EventSourcingTags.ExpectedVersion, expectedVersion);
		}

		return activity;
	}

	/// <summary>
	/// Starts an activity for an event store load operation.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="fromVersion">The version to start loading from (-1 for all events).</param>
	/// <returns>The started activity, or null if no listener is interested.</returns>
	public static Activity? StartLoadActivity(
		string aggregateId,
		string aggregateType,
		long fromVersion = -1)
	{
		var activity = Instance.StartActivity(EventSourcingActivities.Load);
		if (activity != null)
		{
			_ = activity.SetTag(EventSourcingTags.AggregateId, aggregateId);
			_ = activity.SetTag(EventSourcingTags.AggregateType, aggregateType);
			if (fromVersion >= 0)
			{
				_ = activity.SetTag(EventSourcingTags.FromVersion, fromVersion);
			}
		}

		return activity;
	}

	/// <summary>
	/// Starts an activity for retrieving undispatched events.
	/// </summary>
	/// <param name="batchSize">The maximum number of events to retrieve.</param>
	/// <returns>The started activity, or null if no listener is interested.</returns>
	public static Activity? StartGetUndispatchedActivity(int batchSize)
	{
		var activity = Instance.StartActivity(EventSourcingActivities.GetUndispatched);
		_ = (activity?.SetTag(EventSourcingTags.BatchSize, batchSize));
		return activity;
	}

	/// <summary>
	/// Starts an activity for marking an event as dispatched.
	/// </summary>
	/// <param name="eventId">The event identifier.</param>
	/// <returns>The started activity, or null if no listener is interested.</returns>
	public static Activity? StartMarkDispatchedActivity(string eventId)
	{
		var activity = Instance.StartActivity(EventSourcingActivities.MarkDispatched);
		_ = (activity?.SetTag(EventSourcingTags.EventId, eventId));
		return activity;
	}

	/// <summary>
	/// Starts an activity for saving a snapshot.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="version">The snapshot version.</param>
	/// <returns>The started activity, or null if no listener is interested.</returns>
	public static Activity? StartSaveSnapshotActivity(
		string aggregateId,
		string aggregateType,
		long version)
	{
		var activity = Instance.StartActivity(EventSourcingActivities.SaveSnapshot);
		if (activity != null)
		{
			_ = activity.SetTag(EventSourcingTags.AggregateId, aggregateId);
			_ = activity.SetTag(EventSourcingTags.AggregateType, aggregateType);
			_ = activity.SetTag(EventSourcingTags.Version, version);
		}

		return activity;
	}

	/// <summary>
	/// Starts an activity for getting a snapshot.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <returns>The started activity, or null if no listener is interested.</returns>
	public static Activity? StartGetSnapshotActivity(
		string aggregateId,
		string aggregateType)
	{
		var activity = Instance.StartActivity(EventSourcingActivities.GetSnapshot);
		if (activity != null)
		{
			_ = activity.SetTag(EventSourcingTags.AggregateId, aggregateId);
			_ = activity.SetTag(EventSourcingTags.AggregateType, aggregateType);
		}

		return activity;
	}

	/// <summary>
	/// Starts an activity for deleting snapshots.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <returns>The started activity, or null if no listener is interested.</returns>
	public static Activity? StartDeleteSnapshotsActivity(
		string aggregateId,
		string aggregateType)
	{
		var activity = Instance.StartActivity(EventSourcingActivities.DeleteSnapshots);
		if (activity != null)
		{
			_ = activity.SetTag(EventSourcingTags.AggregateId, aggregateId);
			_ = activity.SetTag(EventSourcingTags.AggregateType, aggregateType);
		}

		return activity;
	}

	/// <summary>
	/// Records an exception on the activity and sets its status to error.
	/// </summary>
	/// <param name="activity">The activity to record the exception on.</param>
	/// <param name="exception">The exception to record.</param>
	public static void RecordException(this Activity? activity, Exception exception)
	{
		if (activity != null)
		{
			_ = activity.SetStatus(ActivityStatusCode.Error, exception.Message);
			_ = activity.AddTag(EventSourcingTags.ExceptionType, exception.GetType().FullName);
			_ = activity.AddTag(EventSourcingTags.ExceptionMessage, exception.Message);
		}
	}

	/// <summary>
	/// Sets the operation result tag on the activity.
	/// </summary>
	/// <param name="activity">The activity to set the result on.</param>
	/// <param name="result">The operation result value.</param>
	public static void SetOperationResult(this Activity? activity, string result)
	{
		_ = (activity?.SetTag(EventSourcingTags.OperationResult, result));
	}
}
