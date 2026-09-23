// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
	/// Gets the individual results for each message, one per input, in the order the inputs were given.
	/// </summary>
	/// <value>The per-message send results, index-aligned with the batch's input list.</value>
	/// <remarks>
	/// <para>
	/// <b>This is the batch contract every <see cref="ITransportSender"/> implementation must satisfy.</b>
	/// It is stated here, once, rather than per provider: a contract described separately by each
	/// implementation is not a contract, because a consumer cannot write code that works across them and
	/// the next implementation is bound by nothing.
	/// </para>
	/// <list type="number">
	/// <item>
	/// <b>Exactly one result per input.</b> <c>Results.Count</c> equals the number of messages passed to
	/// <see cref="ITransportSender.SendBatchAsync"/>, and equals
	/// <see cref="SuccessCount"/> + <see cref="FailureCount"/>. No input is omitted, coalesced with
	/// another, or reported twice — including when the send fails wholesale, when the batch is split
	/// across several provider calls, or when the operation is cancelled part way.
	/// </item>
	/// <item>
	/// <b>Index-aligned.</b> <c>Results[i]</c> is the outcome of <c>messages[i]</c>. Results are never
	/// grouped by outcome: an implementation must not return its successes followed by its failures, even
	/// if the underlying client reports them that way, because that ordering destroys the association a
	/// caller needs.
	/// </item>
	/// <item>
	/// <b>Entries that took a fallback path are reported in place.</b> Where a provider cannot fit every
	/// message into one native batch and sends the remainder individually, or retries part of the batch
	/// by another route, each such message's outcome still occupies its own input's index. The path a
	/// message took is an implementation detail; where its result appears is not.
	/// </item>
	/// </list>
	/// <para>
	/// <b>Why it matters:</b> selective retry. On a partial failure a caller resends exactly the inputs
	/// whose result failed. Without index alignment the only safe recovery is to resend the whole batch,
	/// which on an at-least-once transport manufactures duplicates of messages that were already
	/// delivered.
	/// </para>
	/// <para>
	/// <b>Known limitation — a result does not yet carry its input's identity portably.</b>
	/// <see cref="SendResult.MessageId"/> is documented as the broker's identifier, and implementations
	/// differ in practice over whether they report the broker's identifier or the input's own
	/// <see cref="TransportMessage.Id"/>. A caller therefore cannot yet verify alignment from the result
	/// itself, and must rely on position. Until that is settled, treat a provider that groups results by
	/// outcome as a defect to report rather than something to detect at run time.
	/// </para>
	/// </remarks>
	public IReadOnlyList<SendResult> Results { get; init; } = [];

	/// <summary>
	/// Gets the time taken to send the entire batch.
	/// </summary>
	/// <value>The batch duration, or <see langword="null"/> if not measured.</value>
	public TimeSpan? Duration { get; init; }
}
