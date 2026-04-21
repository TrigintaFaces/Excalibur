// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

namespace Excalibur.Inbox.Observability;

/// <summary>
/// Provides centralized activity source and telemetry constants for Inbox tracing operations.
/// </summary>
/// <remarks>
/// <para>
/// This class provides static helper methods for creating OpenTelemetry activity spans
/// for inbox store operations. Activities are automatically linked to parent spans
/// when available.
/// </para>
/// <para>
/// When no listener is attached, all operations are no-ops with zero allocation overhead.
/// </para>
/// <para>
/// All inbox provider packages (SqlServer, Postgres, MongoDB, Redis, ElasticSearch,
/// InMemory, CosmosDb, DynamoDb, Firestore) should use these constants and helpers
/// for consistent telemetry across providers.
/// </para>
/// </remarks>
public static class InboxActivitySource
{
	/// <summary>
	/// The activity source name for Inbox tracing.
	/// </summary>
	public const string Name = "Excalibur.Inbox";

	/// <summary>
	/// The activity source instance for Inbox operations.
	/// Process-lifetime singleton -- do not dispose.
	/// </summary>
	public static ActivitySource Instance { get; } = new(Name);

	/// <summary>
	/// Starts an activity for a CreateEntry inbox operation.
	/// </summary>
	public static Activity? StartCreateEntryActivity(string messageId, string handlerType)
	{
		var activity = Instance.StartActivity(InboxActivities.CreateEntry);
		if (activity != null)
		{
			_ = activity.SetTag(InboxTags.MessageId, messageId);
			_ = activity.SetTag(InboxTags.HandlerType, handlerType);
		}

		return activity;
	}

	/// <summary>
	/// Starts an activity for an ExistsAsync inbox operation.
	/// </summary>
	public static Activity? StartExistsActivity(string messageId, string handlerType)
	{
		var activity = Instance.StartActivity(InboxActivities.Exists);
		if (activity != null)
		{
			_ = activity.SetTag(InboxTags.MessageId, messageId);
			_ = activity.SetTag(InboxTags.HandlerType, handlerType);
		}

		return activity;
	}

	/// <summary>
	/// Starts an activity for a MarkProcessed inbox operation.
	/// </summary>
	public static Activity? StartMarkProcessedActivity(string messageId, string handlerType)
	{
		var activity = Instance.StartActivity(InboxActivities.MarkProcessed);
		if (activity != null)
		{
			_ = activity.SetTag(InboxTags.MessageId, messageId);
			_ = activity.SetTag(InboxTags.HandlerType, handlerType);
		}

		return activity;
	}

	/// <summary>
	/// Starts an activity for a MarkFailed inbox operation.
	/// </summary>
	public static Activity? StartMarkFailedActivity(string messageId, string handlerType)
	{
		var activity = Instance.StartActivity(InboxActivities.MarkFailed);
		if (activity != null)
		{
			_ = activity.SetTag(InboxTags.MessageId, messageId);
			_ = activity.SetTag(InboxTags.HandlerType, handlerType);
		}

		return activity;
	}

	/// <summary>
	/// Starts an activity for a CleanupAsync inbox operation.
	/// </summary>
	public static Activity? StartCleanupActivity()
	{
		return Instance.StartActivity(InboxActivities.Cleanup);
	}
}

/// <summary>
/// Activity names for inbox operations.
/// </summary>
public static class InboxActivities
{
	/// <summary>
	/// Activity name for the create entry operation.
	/// </summary>
	public const string CreateEntry = "inbox.create_entry";

	/// <summary>
	/// Activity name for the exists check operation.
	/// </summary>
	public const string Exists = "inbox.exists";

	/// <summary>
	/// Activity name for the mark processed operation.
	/// </summary>
	public const string MarkProcessed = "inbox.mark_processed";

	/// <summary>
	/// Activity name for the mark failed operation.
	/// </summary>
	public const string MarkFailed = "inbox.mark_failed";

	/// <summary>
	/// Activity name for the cleanup operation.
	/// </summary>
	public const string Cleanup = "inbox.cleanup";
}

/// <summary>
/// Semantic tag names for inbox operations following OpenTelemetry conventions.
/// </summary>
public static class InboxTags
{
	/// <summary>
	/// Tag name for the inbox message identifier.
	/// </summary>
	public const string MessageId = "excalibur.inbox.message_id";

	/// <summary>
	/// Tag name for the handler type processing the message.
	/// </summary>
	public const string HandlerType = "excalibur.inbox.handler_type";

	/// <summary>
	/// Tag name for the type of message being processed.
	/// </summary>
	public const string MessageType = "excalibur.inbox.message_type";

	/// <summary>
	/// Tag name for the inbox provider implementation.
	/// </summary>
	public const string Provider = "excalibur.inbox.provider";
}
