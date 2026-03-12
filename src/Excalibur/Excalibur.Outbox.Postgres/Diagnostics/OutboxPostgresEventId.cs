// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Postgres.Diagnostics;

/// <summary>
/// Event IDs for Postgres outbox store operations (101900-101999, 102000-102099).
/// </summary>
public static class OutboxPostgresEventId
{
	// ========================================
	// 101900-101999: Outbox Store
	// ========================================

	/// <summary>Saving outbox messages.</summary>
	public const int OutboxSaveMessages = 101900;

	/// <summary>Reserving outbox messages.</summary>
	public const int OutboxReserveMessages = 101901;

	/// <summary>Unreserving outbox messages.</summary>
	public const int OutboxUnreserveMessages = 101902;

	/// <summary>Deleting outbox record.</summary>
	public const int OutboxDeleteRecord = 101903;

	/// <summary>Increasing outbox attempts.</summary>
	public const int OutboxIncreaseAttempts = 101904;

	/// <summary>Moving to dead letter.</summary>
	public const int OutboxMoveToDeadLetter = 101905;

	/// <summary>Outbox batch operation.</summary>
	public const int OutboxBatchOperation = 101906;

	/// <summary>Outbox operation completed.</summary>
	public const int OutboxOperationCompleted = 101907;

	/// <summary>Failed to convert outbox message.</summary>
	public const int OutboxConvertMessageFailed = 101908;

	/// <summary>Get failed messages not supported.</summary>
	public const int OutboxGetFailedMessagesNotSupported = 101909;

	/// <summary>Get scheduled messages not supported.</summary>
	public const int OutboxGetScheduledMessagesNotSupported = 101910;

	/// <summary>Cleanup sent messages not needed.</summary>
	public const int OutboxCleanupSentMessagesNotNeeded = 101911;

	/// <summary>Get statistics basic.</summary>
	public const int OutboxGetStatisticsBasic = 101912;

	// ========================================
	// 102000-102099: Dead Letter Store
	// ========================================

	/// <summary>Stored dead letter message.</summary>
	public const int StoredDeadLetterMessage = 102000;

	/// <summary>Marked dead letter message as replayed.</summary>
	public const int MarkedDeadLetterMessageAsReplayed = 102001;

	/// <summary>Deleted dead letter message.</summary>
	public const int DeletedDeadLetterMessage = 102002;

	/// <summary>Cleaned up old dead letter messages.</summary>
	public const int CleanedUpOldDeadLetterMessages = 102003;
}
