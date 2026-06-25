// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// An optional capability for polling <see cref="IOutboxStore"/> implementations that durably record a
/// per-message next-attempt time, so a failed message's computed backoff delay actually throttles when it
/// is re-claimed for retry.
/// </summary>
/// <remarks>
/// <para>
/// This is a segregated capability interface (composition, NOT inheritance of <see cref="IOutboxStore"/>)
/// so that <see cref="IOutboxStore"/> stays within the Interface Segregation threshold, mirroring how
/// <c>IOutboxStoreAdmin</c>, <c>IOutboxStoreBatch</c>, and <see cref="IDeadLetterableOutboxStore"/>
/// segregate optional outbox capabilities.
/// </para>
/// <para>
/// Without it, a failed message is marked <see cref="OutboxStatus.Failed"/> via
/// <see cref="IOutboxStore.MarkFailedAsync(string, string, int, System.Threading.CancellationToken)"/>,
/// which records the failure but cannot carry the next-attempt time -- so the polling claim re-delivers the
/// message as soon as its lease expires and the advertised exponential backoff never throttles re-delivery.
/// The processor computes <c>now + backoffCalculator.CalculateDelay(attempt)</c> and passes the absolute
/// next-attempt time here; the store only persists it. The claim predicate then excludes the message until
/// that time has elapsed (<c>WHERE NextAttemptAt IS NULL OR NextAttemptAt &lt;= @now</c>).
/// </para>
/// <para>
/// Stores that do not implement this capability degrade gracefully: the processor falls back to
/// <see cref="IOutboxStore.MarkFailedAsync(string, string, int, System.Threading.CancellationToken)"/>
/// (today's behavior), so no existing store is broken (the fail-open pattern, matching
/// <see cref="IDeadLetterableOutboxStore"/>).
/// </para>
/// </remarks>
public interface IBackoffSchedulableOutboxStore
{
	/// <summary>
	/// Marks a message as failed and records the time before which it must NOT be re-claimed for retry,
	/// applying the per-message backoff schedule.
	/// </summary>
	/// <remarks>
	/// After this call, the message MUST NOT be returned by the claim predicate
	/// (<see cref="IOutboxStore.GetUnsentMessagesAsync"/>) until <paramref name="nextAttemptAt"/> has
	/// elapsed, so the computed backoff delay genuinely throttles re-delivery.
	/// </remarks>
	/// <param name="messageId">The unique identifier of the message that failed.</param>
	/// <param name="errorMessage">The error description or exception message.</param>
	/// <param name="retryCount">The current retry attempt count.</param>
	/// <param name="nextAttemptAt">
	/// The absolute (computed) time before which the message must not be re-claimed. Typically
	/// <c>now + IBackoffCalculator.CalculateDelay(attempt)</c>, computed by the caller.
	/// </param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task that represents the asynchronous mark-failed-with-backoff operation.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="messageId"/> is null or empty.</exception>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="errorMessage"/> is null.</exception>
	ValueTask MarkFailedWithBackoffAsync(
		string messageId,
		string errorMessage,
		int retryCount,
		DateTimeOffset nextAttemptAt,
		CancellationToken cancellationToken);
}
