// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Represents the result of a batch send operation.
/// Replaces <c>BatchPublishResult</c> with cleaner naming aligned to the <see cref="ITransportSender"/> contract.
/// </summary>
public sealed class BatchSendResult
{
	/// <summary>
	/// Gets the total number of messages in the batch.
	/// </summary>
	/// <value>The total message count.</value>
	public int TotalMessages { get; init; }

	/// <summary>
	/// Gets the number of successfully sent messages.
	/// </summary>
	/// <value>The success count.</value>
	public int SuccessCount { get; init; }

	/// <summary>
	/// Gets the number of failed messages.
	/// </summary>
	/// <value>The failure count.</value>
	public int FailureCount { get; init; }

	/// <summary>
	/// Gets a value indicating whether all messages were sent successfully.
	/// </summary>
	/// <value><see langword="true"/> if all messages succeeded; otherwise, <see langword="false"/>.</value>
	public bool IsCompleteSuccess => FailureCount == 0 && SuccessCount == TotalMessages;

	/// <summary>
	/// Gets the individual results for each message.
	/// </summary>
	/// <value>The per-message send results.</value>
	public IReadOnlyList<SendResult> Results { get; init; } = [];

	/// <summary>
	/// Gets the time taken to send the entire batch.
	/// </summary>
	/// <value>The batch duration, or <see langword="null"/> if not measured.</value>
	public TimeSpan? Duration { get; init; }
}
