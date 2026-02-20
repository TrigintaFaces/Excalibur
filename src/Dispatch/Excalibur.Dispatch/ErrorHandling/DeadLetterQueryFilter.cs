// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Filter criteria for querying dead letter queue entries.
/// </summary>
public sealed class DeadLetterQueryFilter
{
	/// <summary>
	/// Gets or sets the message type to filter by.
	/// </summary>
	public string? MessageType { get; set; }

	/// <summary>
	/// Gets or sets the dead letter reason to filter by.
	/// </summary>
	public DeadLetterReason? Reason { get; set; }

	/// <summary>
	/// Gets or sets the start date for filtering entries.
	/// </summary>
	public DateTimeOffset? FromDate { get; set; }

	/// <summary>
	/// Gets or sets the end date for filtering entries.
	/// </summary>
	public DateTimeOffset? ToDate { get; set; }

	/// <summary>
	/// Gets or sets whether to include only replayed entries (true), only non-replayed entries (false), or all entries (null).
	/// </summary>
	public bool? IsReplayed { get; set; }

	/// <summary>
	/// Gets or sets the source queue to filter by.
	/// </summary>
	public string? SourceQueue { get; set; }

	/// <summary>
	/// Gets or sets the correlation ID to filter by.
	/// </summary>
	public string? CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets the minimum number of original attempts to filter by.
	/// </summary>
	public int? MinAttempts { get; set; }

	/// <summary>
	/// Gets or sets the number of entries to skip (for pagination).
	/// </summary>
	public int Skip { get; set; }

	/// <summary>
	/// Creates a filter for entries with a specific reason.
	/// </summary>
	/// <param name="reason">The dead letter reason to filter by.</param>
	/// <returns>A new filter instance.</returns>
	public static DeadLetterQueryFilter ByReason(DeadLetterReason reason) =>
		new() { Reason = reason };

	/// <summary>
	/// Creates a filter for entries with a specific message type.
	/// </summary>
	/// <param name="messageType">The message type to filter by.</param>
	/// <returns>A new filter instance.</returns>
	public static DeadLetterQueryFilter ByMessageType(string messageType) =>
		new() { MessageType = messageType };

	/// <summary>
	/// Creates a filter for entries within a date range.
	/// </summary>
	/// <param name="from">The start date (inclusive).</param>
	/// <param name="to">The end date (inclusive).</param>
	/// <returns>A new filter instance.</returns>
	public static DeadLetterQueryFilter ByDateRange(DateTimeOffset from, DateTimeOffset to) =>
		new() { FromDate = from, ToDate = to };

	/// <summary>
	/// Creates a filter for entries that have not been replayed.
	/// </summary>
	/// <returns>A new filter instance.</returns>
	public static DeadLetterQueryFilter PendingOnly() =>
		new() { IsReplayed = false };
}
