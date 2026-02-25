// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Result of processing a DLQ message.
/// </summary>
public sealed class DlqProcessingResult
{
	/// <summary>
	/// Gets or sets a value indicating whether the processing was successful.
	/// </summary>
	/// <value>
	/// A value indicating whether the processing was successful.
	/// </value>
	public bool Success { get; set; }

	/// <summary>
	/// Gets or sets the message ID.
	/// </summary>
	/// <value>
	/// The message ID.
	/// </value>
	public required string MessageId { get; set; }

	/// <summary>
	/// Gets or sets the processing action taken.
	/// </summary>
	/// <value>
	/// The processing action taken.
	/// </value>
	public DlqAction Action { get; set; }

	/// <summary>
	/// Gets or sets any error message.
	/// </summary>
	/// <value>
	/// Any error message.
	/// </value>
	public string? ErrorMessage { get; set; }

	/// <summary>
	/// Gets or sets the processing timestamp.
	/// </summary>
	/// <value>
	/// The processing timestamp.
	/// </value>
	public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the number of retry attempts made.
	/// </summary>
	/// <value>
	/// The number of retry attempts made.
	/// </value>
	public int RetryAttempts { get; set; }

	/// <summary>
	/// Gets additional result metadata.
	/// </summary>
	/// <value>
	/// Additional result metadata.
	/// </value>
	public Dictionary<string, object> Metadata { get; } = [];
}
