// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Marten.Diagnostics;

/// <summary>
/// Event IDs for Marten outbox store operations (102400-102499).
/// </summary>
public static class OutboxMartenEventId
{
	/// <summary>Staged an outbox message.</summary>
	public const int OutboxMessageStaged = 102400;

	/// <summary>Enqueued an outbox message.</summary>
	public const int OutboxMessageEnqueued = 102401;

	/// <summary>Marked an outbox message as sent.</summary>
	public const int OutboxMessageSent = 102402;

	/// <summary>Marked an outbox message as failed.</summary>
	public const int OutboxMessageFailed = 102403;

	/// <summary>Cleaned up sent outbox messages.</summary>
	public const int OutboxMessagesCleanedUp = 102404;

	/// <summary>Failed to convert a Marten document to an outbound message.</summary>
	public const int OutboxConvertMessageFailed = 102405;
}
