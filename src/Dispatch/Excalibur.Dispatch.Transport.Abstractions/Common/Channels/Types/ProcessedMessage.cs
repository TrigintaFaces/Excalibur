// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Processed message information.
/// </summary>
public sealed class ProcessedMessage
{
	/// <summary>
	/// Gets or sets the message ID.
	/// </summary>
	/// <value>The current <see cref="MessageId"/> value.</value>
	public string MessageId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the processing time.
	/// </summary>
	/// <value>The current <see cref="ProcessedAt"/> value.</value>
	public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the processing duration.
	/// </summary>
	/// <value>The current <see cref="Duration"/> value.</value>
	public TimeSpan Duration { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether processing was successful.
	/// </summary>
	/// <value>The current <see cref="Success"/> value.</value>
	public bool Success { get; set; }

	/// <summary>
	/// Gets or sets any error message.
	/// </summary>
	/// <value>The current <see cref="ErrorMessage"/> value.</value>
	public string? ErrorMessage { get; set; }
}
