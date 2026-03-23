// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

namespace Excalibur.Cdc.Diagnostics;

/// <summary>
/// Provides centralized activity source for CDC (Change Data Capture) tracing operations.
/// </summary>
/// <remarks>
/// <para>
/// This class provides static helper methods for creating OpenTelemetry activity spans
/// for CDC operations. All CDC provider packages (SqlServer, Postgres, MongoDB, CosmosDb,
/// DynamoDb, Firestore) should use these constants and helpers for consistent telemetry.
/// </para>
/// <para>
/// When no listener is attached, all operations are no-ops with zero allocation overhead.
/// </para>
/// </remarks>
public static class CdcActivitySource
{
	/// <summary>
	/// The activity source name for CDC tracing.
	/// </summary>
	public const string Name = "Excalibur.Cdc";

	/// <summary>
	/// The activity source instance for CDC operations.
	/// Process-lifetime singleton -- do not dispose.
	/// </summary>
	public static ActivitySource Instance { get; } = new(Name);

	/// <summary>
	/// Starts an activity for a CDC polling cycle.
	/// </summary>
	/// <param name="provider">The CDC provider name (e.g., "Postgres", "MongoDB").</param>
	public static Activity? StartPollActivity(string provider)
	{
		var activity = Instance.StartActivity(CdcActivities.Poll);
		if (activity != null)
		{
			_ = activity.SetTag(CdcTags.Provider, provider);
		}

		return activity;
	}

	/// <summary>
	/// Starts an activity for processing a batch of CDC changes.
	/// </summary>
	/// <param name="provider">The CDC provider name.</param>
	/// <param name="batchSize">The number of changes in the batch.</param>
	public static Activity? StartProcessBatchActivity(string provider, int batchSize)
	{
		var activity = Instance.StartActivity(CdcActivities.ProcessBatch);
		if (activity != null)
		{
			_ = activity.SetTag(CdcTags.Provider, provider);
			_ = activity.SetTag(CdcTags.BatchSize, batchSize);
		}

		return activity;
	}

	/// <summary>
	/// Starts an activity for applying a single CDC change.
	/// </summary>
	/// <param name="provider">The CDC provider name.</param>
	/// <param name="changeType">The type of change (insert, update, delete).</param>
	public static Activity? StartApplyChangeActivity(string provider, string changeType)
	{
		var activity = Instance.StartActivity(CdcActivities.ApplyChange);
		if (activity != null)
		{
			_ = activity.SetTag(CdcTags.Provider, provider);
			_ = activity.SetTag(CdcTags.ChangeType, changeType);
		}

		return activity;
	}

	/// <summary>
	/// Starts an activity for saving a CDC checkpoint.
	/// </summary>
	/// <param name="provider">The CDC provider name.</param>
	public static Activity? StartCheckpointActivity(string provider)
	{
		var activity = Instance.StartActivity(CdcActivities.Checkpoint);
		if (activity != null)
		{
			_ = activity.SetTag(CdcTags.Provider, provider);
		}

		return activity;
	}
}

/// <summary>
/// Activity names for CDC operations.
/// </summary>
public static class CdcActivities
{
	/// <summary>A CDC polling cycle.</summary>
	public const string Poll = "cdc.poll";

	/// <summary>Processing a batch of CDC changes.</summary>
	public const string ProcessBatch = "cdc.process_batch";

	/// <summary>Applying a single CDC change.</summary>
	public const string ApplyChange = "cdc.apply_change";

	/// <summary>Saving a CDC checkpoint/position.</summary>
	public const string Checkpoint = "cdc.checkpoint";
}

/// <summary>
/// Semantic tag names for CDC operations following OpenTelemetry conventions.
/// </summary>
public static class CdcTags
{
	/// <summary>The CDC provider name.</summary>
	public const string Provider = "excalibur.cdc.provider";

	/// <summary>The batch size.</summary>
	public const string BatchSize = "excalibur.cdc.batch_size";

	/// <summary>The change type (insert, update, delete).</summary>
	public const string ChangeType = "excalibur.cdc.change_type";

	/// <summary>The capture instance or collection name.</summary>
	public const string CaptureInstance = "excalibur.cdc.capture_instance";
}
