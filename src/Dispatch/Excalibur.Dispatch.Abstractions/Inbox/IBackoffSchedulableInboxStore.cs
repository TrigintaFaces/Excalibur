// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Dispatch;

/// <summary>
/// An optional capability for <see cref="IInboxStore"/> implementations that durably record a
/// per-entry next-attempt time, so a failed message's computed backoff delay actually throttles when it
/// is re-claimed for retry.
/// </summary>
/// <remarks>
/// <para>
/// This is a segregated capability interface (composition, NOT inheritance of <see cref="IInboxStore"/>)
/// so that <see cref="IInboxStore"/> stays within the Interface Segregation threshold, mirroring how
/// <see cref="IBackoffSchedulableOutboxStore"/> segregates the equivalent outbox capability.
/// </para>
/// <para>
/// Unlike the outbox (keyed by message id alone), the inbox is keyed by the composite
/// <c>(messageId, handlerType)</c> -- the same message can be processed independently by multiple
/// handlers -- so the backoff schedule is recorded per <c>(messageId, handlerType)</c> entry and carries
/// the current <c>retryCount</c> the processor used to compute the delay.
/// </para>
/// <para>
/// Without it, a failed entry is marked <see cref="InboxStatus.Failed"/> via
/// <see cref="IInboxStore.MarkFailedAsync(string, string, string, System.Threading.CancellationToken)"/>,
/// which records the failure but cannot carry the next-attempt time -- so the re-admission claim
/// re-delivers the entry on a fixed window and the advertised exponential backoff never throttles
/// re-delivery. The processor computes <c>now + IBackoffCalculator.CalculateDelay(attempt)</c> and passes
/// the absolute next-attempt time here; the store only persists it. The claim predicate then excludes the
/// entry until that time has elapsed (<c>WHERE NextAttemptAt IS NULL OR NextAttemptAt &lt;= @now</c>).
/// </para>
/// <para>
/// Stores that do not implement this capability degrade gracefully: the processor falls back to
/// <see cref="IInboxStore.MarkFailedAsync(string, string, string, System.Threading.CancellationToken)"/>
/// and its existing fixed re-admission window (today's behavior), so no existing store is broken (the
/// fail-open pattern, matching <see cref="IBackoffSchedulableOutboxStore"/>).
/// </para>
/// </remarks>
public interface IBackoffSchedulableInboxStore
{
	/// <summary>
	/// Marks an inbox entry as failed for a specific handler and records the time before which it must NOT
	/// be re-claimed for retry, applying the per-entry backoff schedule.
	/// </summary>
	/// <remarks>
	/// After this call, the entry MUST NOT be returned by the re-admission claim
	/// (<see cref="IInboxStoreAdmin.GetAllTenantsFailedEntriesAsync"/>) until <paramref name="nextAttemptAt"/> has
	/// elapsed, so the computed backoff delay genuinely throttles re-delivery.
	/// </remarks>
	/// <param name="messageId">The unique identifier of the message that failed.</param>
	/// <param name="handlerType">The fully qualified type name of the handler that failed.</param>
	/// <param name="errorMessage">The error description or exception message.</param>
	/// <param name="retryCount">The current retry attempt count.</param>
	/// <param name="nextAttemptAt">
	/// The absolute (computed) time before which the entry must not be re-claimed. Typically
	/// <c>now + IBackoffCalculator.CalculateDelay(attempt)</c>, computed by the caller.
	/// </param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// <see cref="InboxMarkFailedOutcome.Applied"/> when the entry was mutated and the backoff was scheduled
	/// — scheduling is part of applying, so it has no separate outcome;
	/// <see cref="InboxMarkFailedOutcome.EntryNotFound"/> when no such entry exists in the caller's tenant
	/// scope; <see cref="InboxMarkFailedOutcome.AlreadyProcessed"/> when the entry is present but terminal
	/// and the transition was refused, in which case no backoff is scheduled either.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Carries the same three obligations as <see cref="IInboxStore.MarkFailedAsync"/> <b>about the state of
	/// an entry</b>: an absent entry returns <see cref="InboxMarkFailedOutcome.EntryNotFound"/> rather than
	/// throwing, a refused terminal entry is reported rather than silently ignored, and the outcome is
	/// decided by the statement that performs the write rather than by a read taken after it.
	/// </para>
	/// <para>
	/// <b>Those obligations are about the ENTRY. They do not cover the capability itself being absent.</b>
	/// A decorator may implement this interface in order to FORWARD it and still wrap an inner store that
	/// cannot schedule. Such a decorator reports <see cref="IInboxStoreCapabilities.SupportsBackoffScheduling"/>
	/// as <see langword="false"/>, and calling this member anyway is a programming error — the caller ignored
	/// the documented probe — so it throws <see cref="NotSupportedException"/> rather than degrading to a
	/// plain mark-failed. Scheduling is part of applying here, so a silent degrade would return
	/// <see cref="InboxMarkFailedOutcome.Applied"/> while asserting a schedule that does not exist, and
	/// nothing downstream would ever learn otherwise.
	/// </para>
	/// <para>
	/// <b>Consumer obligation: probe <see cref="IInboxStoreCapabilities.SupportsBackoffScheduling"/> before
	/// calling this member.</b> A bare type test is not sufficient — it is satisfied by a forwarding
	/// decorator whose inner store lacks the capability.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">Thrown when <paramref name="messageId"/> or <paramref name="handlerType"/> is null or empty.</exception>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="errorMessage"/> is null.</exception>
	/// <exception cref="NotSupportedException">
	/// Thrown when the implementation declares this capability in order to forward it but the store behind it
	/// cannot schedule a backoff — that is, when
	/// <see cref="IInboxStoreCapabilities.SupportsBackoffScheduling"/> is <see langword="false"/>.
	/// </exception>
	ValueTask<InboxMarkFailedOutcome> MarkFailedWithBackoffAsync(
		string messageId,
		string handlerType,
		string errorMessage,
		int retryCount,
		DateTimeOffset nextAttemptAt,
		CancellationToken cancellationToken);
}
