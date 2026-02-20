// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Filter criteria for querying dead letter messages.
/// </summary>
public sealed class DeadLetterFilter
{
	/// <summary>
	/// Gets or sets the message type to filter by.
	/// </summary>
	/// <value>The current <see cref="MessageType"/> value.</value>
	public string? MessageType { get; set; }

	/// <summary>
	/// Gets or sets the reason to filter by.
	/// </summary>
	/// <value>The current <see cref="Reason"/> value.</value>
	public string? Reason { get; set; }

	/// <summary>
	/// Gets or sets the start date for filtering.
	/// </summary>
	/// <value>The current <see cref="FromDate"/> value.</value>
	public DateTimeOffset? FromDate { get; set; }

	/// <summary>
	/// Gets or sets the end date for filtering.
	/// </summary>
	/// <value>The current <see cref="ToDate"/> value.</value>
	public DateTimeOffset? ToDate { get; set; }

	/// <summary>
	/// Gets or sets whether to include only replayed messages.
	/// </summary>
	/// <value>The current <see cref="IsReplayed"/> value.</value>
	public bool? IsReplayed { get; set; }

	/// <summary>
	/// Gets or sets the source system to filter by.
	/// </summary>
	/// <value>The current <see cref="SourceSystem"/> value.</value>
	public string? SourceSystem { get; set; }

	/// <summary>
	/// Gets or sets the correlation ID to filter by.
	/// </summary>
	/// <value>The current <see cref="CorrelationId"/> value.</value>
	public string? CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of results to return.
	/// </summary>
	/// <value>The current <see cref="MaxResults"/> value.</value>
	public int MaxResults { get; set; } = 100;

	/// <summary>
	/// Gets or sets the number of results to skip for pagination.
	/// </summary>
	/// <value>The current <see cref="Skip"/> value.</value>
	public int Skip { get; set; }
}
