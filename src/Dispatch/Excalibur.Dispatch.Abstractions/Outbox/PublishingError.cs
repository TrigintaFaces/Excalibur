// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents an error that occurred during message publishing.
/// </summary>
/// <remarks> Creates a new publishing error. </remarks>
/// <param name="messageId"> The ID of the message that failed. </param>
/// <param name="error"> The error message. </param>
/// <param name="exception"> The exception that caused the failure. </param>
public sealed class PublishingError(string messageId, string error, Exception? exception = null)
{
	/// <summary>
	/// Gets the ID of the message that failed to publish.
	/// </summary>
	public string MessageId { get; init; } = messageId ?? throw new ArgumentNullException(nameof(messageId));

	/// <summary>
	/// Gets the error message or exception details.
	/// </summary>
	public string Error { get; init; } = error ?? throw new ArgumentNullException(nameof(error));

	/// <summary>
	/// Gets the exception that caused the failure, if available.
	/// </summary>
	/// <value> The current <see cref="Exception" /> value. </value>
	public Exception? Exception { get; init; } = exception;

	/// <summary>
	/// Gets the timestamp when the error occurred.
	/// </summary>
	/// <value> The current <see cref="Timestamp" /> value. </value>
	public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public override string ToString() => $"PublishingError[{MessageId}]: {Error}";
}
