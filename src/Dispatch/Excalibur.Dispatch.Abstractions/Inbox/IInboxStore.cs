// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Dispatch;

/// <summary>
/// Provides persistent storage for incoming messages to ensure at-most-once processing semantics.
/// </summary>
/// <remarks>
/// <para>
/// The inbox store implements the Idempotent Consumer pattern by persistently tracking processed messages
/// before handler execution. Within one tenant scope, messages are keyed by a composite of
/// <c>(messageId, handlerType)</c>, allowing the same message to be processed independently by multiple
/// handlers.
/// </para>
/// <para>This ensures:</para>
/// <list type="bullet">
/// <item><description>Duplicate detection - each handler processes a message at most once</description></item>
/// <item><description>Resilience - processing state survives application restarts</description></item>
/// <item><description>Consistency - messages are marked processed only after successful handling</description></item>
/// <item><description>Audit trail - complete processing history is maintained</description></item>
/// <item><description>Multi-handler support - different handlers can process the same message independently</description></item>
/// </list>
/// <para>
/// <b>Durability fault model.</b> Every mutating operation is <em>fail-loud</em>: a successful (non-faulted)
/// return guarantees the corresponding state change was durably recorded before the returned task completed,
/// and a persistence failure is surfaced as a thrown exception — never swallowed as a silent no-op. Callers
/// may therefore treat a completed <see cref="MarkProcessedAsync"/> / <see cref="TryMarkAsProcessedAsync"/>
/// as proof that the dedup record is persisted, so at-least-once redelivery combined with an idempotent
/// handler yields at-most-once effects even across a process crash.
/// </para>
/// <para>
/// <b>Ownership.</b> Every member here resolves <c>(messageId, handlerType)</c> <em>within the caller's
/// current tenant scope</em>. That scope is ambient rather than a parameter — no method on this interface
/// takes a tenant argument — so the logical dedup identity is <c>(tenant scope, messageId, handlerType)</c>.
/// An implementation MUST NOT let one scope observe, claim, or mutate another scope's record: a record owned
/// by a different tenant is invisible to every member of this interface, exactly as if it did not exist.
/// Reporting an existing record on the strength of another tenant's row would make the host acknowledge a
/// message it never handled — a silent loss, not a duplicate.
/// </para>
/// <para>
/// Two registrations satisfy that requirement. Registered without multi-tenancy, the store <em>is</em> the
/// scope: one untenanted namespace keyed by <c>(messageId, handlerType)</c>. Registered with multi-tenancy,
/// the tenant is part of the store's unique key and is never absent. A provider that cannot apply the ambient
/// tenant is refused when multi-tenancy is registered, rather than degrading silently at runtime, so a caller
/// may rely on the guarantee above without knowing which provider is wired.
/// </para>
/// <para>
/// Interface uses ValueTask for synchronous completion optimization.
/// In-memory implementations complete synchronously without allocation overhead.
/// </para>
/// <para>
/// The tenant obligation above is declared at this contract, not only named in the composition-time
/// gate. The order-independent startup re-assertion selects its subjects by that declaration, so a
/// contract covered only by a sweep of the service collection taken at the instant multi-tenancy is
/// registered is not covered against a store registered afterwards -- an ordinary arrangement when
/// persistence is added by a later feature module.
/// </para>
/// </remarks>
[TenantOwned]
public interface IInboxStore
{
	/// <summary>
	/// Creates a new inbox entry for an incoming message and handler combination.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="handlerType">The deduplication scope the entry is keyed under, together with <paramref name="messageId"/>. See <see cref="InboxEntry.HandlerType"/>.</param>
	/// <param name="messageType">A type name for the message that the message type registry can resolve. See <see cref="InboxEntry.MessageType"/>.</param>
	/// <param name="payload">The serialized message payload.</param>
	/// <param name="metadata">Additional message metadata including headers and context.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The created inbox entry with generated timestamps and initial status.</returns>
	/// <exception cref="ArgumentException">Thrown when messageId, handlerType, or messageType is null or empty.</exception>
	/// <exception cref="ArgumentNullException">Thrown when payload or metadata is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when an entry with the same (messageId, handlerType) already exists.</exception>
	ValueTask<InboxEntry> CreateEntryAsync(
		string messageId,
		string handlerType,
		string messageType,
		byte[] payload,
		IDictionary<string, object> metadata,
		CancellationToken cancellationToken);

	/// <summary>
	/// Marks a message as successfully processed for a specific handler.
	/// </summary>
	/// <remarks>
	/// When this operation completes without faulting, the processed marker is guaranteed to be durably
	/// persisted. A persistence failure MUST propagate as a thrown exception; the operation MUST NOT return
	/// successfully while leaving the marker unrecorded (a silent no-op would break dedup across a crash).
	/// </remarks>
	/// <param name="messageId">The unique identifier of the message to mark as processed.</param>
	/// <param name="handlerType">The deduplication scope the entry is keyed under, together with <paramref name="messageId"/>. See <see cref="InboxEntry.HandlerType"/>.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task that represents the asynchronous mark-processed operation.</returns>
	/// <exception cref="ArgumentException">Thrown when messageId or handlerType is null or empty.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the entry does not exist or is already processed.</exception>
	ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken);

	/// <summary>
	/// Atomically attempts to mark a message as processed for a specific handler.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This method provides atomic "first writer wins" semantics for idempotent message processing.
	/// If no record exists for this message and handler, it creates the entry and returns <c>true</c>.
	/// If one already exists, it returns <c>false</c> without throwing.
	/// </para>
	/// <para>
	/// This is the preferred method for idempotent message handling as it combines the check-and-mark
	/// operation atomically, preventing race conditions in concurrent processing scenarios.
	/// </para>
	/// <para>
	/// The <see langword="bool"/> result is only returned once the atomic claim has been durably persisted:
	/// a <c>true</c> result guarantees this caller is the recorded first writer, and a <c>false</c> result
	/// guarantees an existing durable record <em>for this handler in the caller's own tenant scope</em> —
	/// never a record owned by another tenant. A persistence failure MUST throw rather than return a value, so
	/// the boolean decision is never backed by unpersisted state.
	/// </para>
	/// </remarks>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="handlerType">The deduplication scope the entry is keyed under, together with <paramref name="messageId"/>. See <see cref="InboxEntry.HandlerType"/>.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// <c>true</c> if this call created the entry and is therefore the recorded first writer for this
	/// message and handler in the caller's tenant scope; <c>false</c> if a durable record already exists in
	/// that same scope, in which case the caller MUST NOT treat itself as the first writer.
	/// <para>
	/// <c>false</c> does <b>not</b> identify who wrote the record. It may have been another delivery, or it
	/// may have been an earlier attempt by this same caller whose commit succeeded but whose response was
	/// lost and which the provider then retried. The record carries no writer identity, so the two cases are
	/// indistinguishable here. Read <c>false</c> as "a record exists — do not claim first-writer status",
	/// never as "someone else has already done the work, so this caller may skip it".
	/// </para>
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when messageId or handlerType is null or empty.</exception>
	ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken);

	/// <summary>
	/// Checks if a message has already been processed by a specific handler.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message to check.</param>
	/// <param name="handlerType">The deduplication scope the entry is keyed under, together with <paramref name="messageId"/>. See <see cref="InboxEntry.HandlerType"/>.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// <c>true</c> if the message has been processed by this handler in the caller's tenant scope; otherwise,
	/// <c>false</c>. A record owned by another tenant never yields <c>true</c>.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when messageId or handlerType is null or empty.</exception>
	ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves an inbox entry by message identifier and handler type.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message to retrieve.</param>
	/// <param name="handlerType">The deduplication scope the entry is keyed under, together with <paramref name="messageId"/>. See <see cref="InboxEntry.HandlerType"/>.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The inbox entry if found; otherwise, <c>null</c>.</returns>
	/// <exception cref="ArgumentException">Thrown when messageId or handlerType is null or empty.</exception>
	ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken);

	/// <summary>
	/// Marks a message as failed during processing for a specific handler, and reports whether the entry
	/// was actually mutated.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The transition can be refused, so it is reported rather than thrown.</b> Processed is absorbing:
	/// an entry that already completed must not be demoted to failed, because that would re-admit it to the
	/// drain and run its handler again over side effects already committed. A refusal is therefore an
	/// ordinary and expected answer on this path, not a fault.
	/// </para>
	/// <para>
	/// <b>An absent entry returns <see cref="InboxMarkFailedOutcome.EntryNotFound"/> and MUST NOT throw.</b>
	/// A store that throws here cannot be substituted for one that does not: a caller written to catch is
	/// broken against the silent stores and a caller written not to is broken against the throwing ones, so
	/// no caller can be correct against the interface.
	/// </para>
	/// <para>
	/// <b>THROW ONLY WHEN THE STORE COULD NOT BE ASKED.</b> That is the rule, and it is stated as a rule
	/// rather than as a list of examples because a list invites the next case to be fitted into the nearest
	/// entry. If the store answered — including answering that it could not decide — the answer is a return
	/// value. If the store could not be reached, or the statement could not be issued, that is an exception.
	/// </para>
	/// <para>
	/// The case that makes the distinction concrete is <b>contention</b>. A store whose transition needs an
	/// optimistic-concurrency loop can lose every attempt to a competing writer. It was asked, it answered,
	/// and nothing was written, so it returns <see cref="InboxMarkFailedOutcome.Undecided"/> and does not
	/// throw — the entry is exactly as it was found and the call is safe to re-drive. Throwing there is
	/// actively harmful: this member is normally called while the caller is already handling a failure, so
	/// an exception raised here replaces the consumer's original error with one about the store.
	/// </para>
	/// <para>
	/// <b>The outcome is decided by the statement that performs the write, never by a read taken afterwards.</b>
	/// Classifying from a follow-up read reintroduces the very race the outcome exists to remove: between the
	/// write and the read another worker can complete the entry, and the caller is then told a refusal that
	/// did not happen, or an application that did not.
	/// </para>
	/// </remarks>
	/// <param name="messageId">The unique identifier of the message that failed.</param>
	/// <param name="handlerType">The deduplication scope the entry is keyed under, together with <paramref name="messageId"/>. See <see cref="InboxEntry.HandlerType"/>.</param>
	/// <param name="errorMessage">The error description or exception message.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// <see cref="InboxMarkFailedOutcome.Applied"/> when the entry was mutated;
	/// <see cref="InboxMarkFailedOutcome.EntryNotFound"/> when no such entry exists in the caller's tenant
	/// scope; <see cref="InboxMarkFailedOutcome.AlreadyProcessed"/> when the entry is present but terminal
	/// and the transition was refused; <see cref="InboxMarkFailedOutcome.Undecided"/> when the store was
	/// asked, wrote nothing, and could not reach a decision — characteristically an exhausted
	/// optimistic-concurrency loop. Treat anything other than
	/// <see cref="InboxMarkFailedOutcome.Applied"/> as not applied.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when messageId or handlerType is null or empty.</exception>
	/// <exception cref="ArgumentNullException">Thrown when errorMessage is null.</exception>
	ValueTask<InboxMarkFailedOutcome> MarkFailedAsync(string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken);
}
