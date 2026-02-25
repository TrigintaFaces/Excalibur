// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Represents the result of a message send operation.
/// Replaces <c>PublishResult</c> with cleaner naming aligned to the <see cref="ITransportSender"/> contract.
/// </summary>
public sealed class SendResult
{
	/// <summary>
	/// Gets a value indicating whether the send operation was successful.
	/// </summary>
	/// <value><see langword="true"/> if the send succeeded; otherwise, <see langword="false"/>.</value>
	public bool IsSuccess { get; init; }

	/// <summary>
	/// Gets the message ID assigned by the broker.
	/// </summary>
	/// <value>The broker-assigned message identifier.</value>
	public string? MessageId { get; init; }

	/// <summary>
	/// Gets the sequence number assigned by the broker (if supported).
	/// </summary>
	/// <value>The sequence number, or <see langword="null"/> if not applicable.</value>
	public long? SequenceNumber { get; init; }

	/// <summary>
	/// Gets the partition or shard where the message was stored.
	/// </summary>
	/// <value>The partition identifier, or <see langword="null"/> if not applicable.</value>
	public string? Partition { get; init; }

	/// <summary>
	/// Gets the timestamp when the message was accepted by the broker.
	/// </summary>
	/// <value>The acceptance timestamp, or <see langword="null"/> if not available.</value>
	public DateTimeOffset? AcceptedAt { get; init; }

	/// <summary>
	/// Gets the error information if the send failed.
	/// </summary>
	/// <value>The error details, or <see langword="null"/> if the send succeeded.</value>
	public SendError? Error { get; init; }

	/// <summary>
	/// Creates a successful send result.
	/// </summary>
	/// <param name="messageId">The message ID assigned by the broker.</param>
	/// <returns>A successful <see cref="SendResult"/>.</returns>
	public static SendResult Success(string messageId) =>
		new() { IsSuccess = true, MessageId = messageId, AcceptedAt = DateTimeOffset.UtcNow };

	/// <summary>
	/// Creates a failed send result.
	/// </summary>
	/// <param name="error">The error that occurred.</param>
	/// <returns>A failed <see cref="SendResult"/>.</returns>
	public static SendResult Failure(SendError error) =>
		new() { IsSuccess = false, Error = error };
}
