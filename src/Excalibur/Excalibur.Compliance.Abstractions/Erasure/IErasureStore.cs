// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Dispatch;

namespace Excalibur.Compliance;

/// <summary>
/// Storage abstraction for erasure requests and certificates.
/// </summary>
/// <remarks>
/// <para>
/// Implementations should use Dapper (NOT EntityFramework Core) per project constraints.
/// </para>
/// <para>
/// <b>Failures are part of this contract, not an implementation detail.</b> The members below state the
/// exception each one raises, and every implementation is required to raise the same exception for the
/// same condition — a caller must be able to write one <c>catch</c> that works against any store. In
/// particular, callers should not have to catch a database provider's own exception type: an
/// implementation that lets a raw provider exception escape for a condition named here is not conforming,
/// because it forces every consumer to reference that provider and to know its error codes.
/// </para>
/// <para>
/// <b>One condition, one exception type — and the type must identify the condition.</b> Where several
/// conditions would otherwise share a type, each gets its own: a duplicate request identifier raises
/// <see cref="DuplicateErasureRequestException"/>, an absent or stale backing schema raises
/// <see cref="ErasureStoreNotProvisionedException"/> (outside the <see cref="InvalidOperationException"/>
/// hierarchy entirely), and an unresolved ambient tenant raises <see cref="TenantRequiredException"/>. A
/// caller branching on the duplicate signal MUST catch that specific type. Catching
/// <see cref="InvalidOperationException"/> instead reads every one of those conditions as "the request is
/// already on file", so a request that was never stored is treated as stored and never re-filed — and an
/// erasure request dropped that way is a statutory right silently lost, with nothing anywhere reporting it.
/// </para>
/// <para>
/// <b>Two different ways of reporting "not found" appear here, deliberately.</b>
/// <see cref="UpdateStatusAsync"/> and <see cref="RecordCancellationAsync"/> return <see langword="false"/>
/// when no matching request exists, because for those operations a missing request is an ordinary outcome
/// a caller is expected to branch on. <see cref="UpdateStatusAsync"/> returns it for a second reason as
/// well — a refused claim, described on that member — and a caller that needs to tell the two apart must
/// read the status rather than infer it from the <see langword="false"/>. <see cref="RecordCompletionAsync"/> throws instead, because
/// recording a completion asserts that an erasure actually happened: silently succeeding would attest to
/// erasing data that was never requested, and that attestation is the evidence a data subject or a
/// regulator is ultimately shown.
/// </para>
/// <para>
/// <b>Tenant confinement.</b> Every member here is confined to the ambient tenant established for this
/// store instance. A <c>requestId</c>-addressed member (<see cref="GetStatusAsync"/>,
/// <see cref="UpdateStatusAsync"/>, <see cref="RecordCompletionAsync"/>,
/// <see cref="RecordCancellationAsync"/>) reports another tenant's request the same way it reports one
/// that never existed — not found — rather than resolving or mutating it, so a caller cannot probe for a
/// foreign request's existence by its identifier alone. Which mechanism a given provider uses to hold
/// that boundary is declared by its capability marker — <see cref="ITenantScopingCapability{TContract}"/>
/// for a store that reads an ambient tenant — and the package's own <c>ARCHITECTURE.md</c> states the
/// falsifiable guarantee and how it is verified. A store presenting no marker is not confined by the
/// framework.
/// </para>
/// </remarks>
[TenantOwned]
public interface IErasureStore
{
	/// <summary>
	/// Saves a new erasure request.
	/// </summary>
	/// <param name="request">The erasure request to save.</param>
	/// <param name="scheduledExecutionTime">When the erasure is scheduled to execute.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the async operation.</returns>
	/// <exception cref="DuplicateErasureRequestException">
	/// A request with the same <see cref="ErasureRequest.RequestId"/> already exists — this exception is
	/// raised for that condition and for no other, so it is the signal a caller may safely read as "already
	/// on file". This operation inserts; it does not overwrite. Re-filing an existing request identifier is
	/// reported rather than silently replacing the stored request, because the original request records when
	/// erasure was asked for and under which legal basis.
	/// </exception>
	/// <exception cref="ErasureStoreNotProvisionedException">
	/// The store's backing schema is absent, or is present but missing columns its statements bind. A
	/// deployment fault rather than an outcome of this call: nothing about <paramref name="request"/> is
	/// wrong, the request was <b>not</b> stored, and re-filing it after the schema is repaired is correct.
	/// </exception>
	/// <exception cref="TenantRequiredException">
	/// Multi-tenancy is active but no ambient tenant is established, so the request cannot be confined to a
	/// tenant. The request was <b>not</b> stored.
	/// </exception>
	/// <exception cref="ObjectDisposedException">
	/// The store has been disposed. The request was <b>not</b> stored.
	/// </exception>
	Task SaveRequestAsync(
		ErasureRequest request,
		DateTimeOffset scheduledExecutionTime,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the status of an erasure request.
	/// </summary>
	/// <param name="requestId">The request ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The erasure status, or null if not found.</returns>
	/// <remarks>
	/// Confined to the ambient tenant established for this store instance: a request stored under
	/// another tenant is reported as not found, the same as a <paramref name="requestId"/> that never
	/// existed.
	/// </remarks>
	/// <exception cref="ErasureStoreNotProvisionedException">
	/// The store's backing schema is absent or stale, so the lookup cannot be answered at all. This is the
	/// only condition under which this member does not return: a request that is not there is reported as
	/// <see langword="null"/> and never as an exception, so a caller can always tell "no such request" from
	/// "this store cannot answer".
	/// </exception>
	/// <exception cref="TenantRequiredException">
	/// Multi-tenancy is active but no ambient tenant is established, so the lookup cannot be confined to a
	/// tenant. It fails closed rather than reading across tenants.
	/// </exception>
	Task<ErasureStatus?> GetStatusAsync(
		Guid requestId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Updates the status of an erasure request.
	/// </summary>
	/// <param name="requestId">The request ID.</param>
	/// <param name="status">The new status.</param>
	/// <remarks>
	/// <para>
	/// <b>The transition to <see cref="ErasureRequestStatus.InProgress"/> is a CLAIM, and it is
	/// conditional.</b> It succeeds only for a request currently in
	/// <see cref="ErasureRequestStatus.Scheduled"/>, and the test and the write are ONE indivisible
	/// operation — a conditional update whose predicate names the expected prior state, never a read
	/// followed by a write. Two callers racing to execute the same request must not both be told they
	/// claimed it: exactly one receives <see langword="true"/> and the other <see langword="false"/>.
	/// An implementation that updates the row unconditionally satisfies every single-threaded test and
	/// grants the claim to both racers, each of which then erases the same subject and writes its own
	/// completion certificate — one of them attesting that keys were destroyed when its own attempt
	/// found nothing left to destroy.
	/// </para>
	/// <para>
	/// Every other transition is unconditional: a request may be moved to a terminal or blocked state
	/// from whatever state it is in, because those transitions record an outcome rather than acquire
	/// ownership.
	/// </para>
	/// </remarks>
	/// <param name="errorMessage">Optional error message if failed.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
/// <see langword="true"/> if the row was updated; <see langword="false"/> if no matching request exists,
/// or if the transition was refused because the request was not in the state this transition requires.
/// </returns>
	/// <exception cref="ErasureStoreNotProvisionedException">
	/// The store's backing schema is absent or stale, so the update cannot be attempted at all. This is the
	/// only condition under which this member does not return: a request that is not there is reported as
	/// <see langword="false"/> and never as an exception, so a caller can always tell "no such request" from
	/// "this store cannot answer".
	/// </exception>
	/// <exception cref="TenantRequiredException">
	/// Multi-tenancy is active but no ambient tenant is established, so the update cannot be confined to a
	/// tenant. It fails closed rather than mutating across tenants.
	/// </exception>
	Task<bool> UpdateStatusAsync(
		Guid requestId,
		ErasureRequestStatus status,
		string? errorMessage,
		CancellationToken cancellationToken);

	/// <summary>
	/// Records erasure completion.
	/// </summary>
	/// <param name="requestId">The request ID.</param>
	/// <param name="keysDeleted">Number of keys deleted.</param>
	/// <param name="recordsAffected">Number of records affected.</param>
	/// <param name="certificateId">The generated certificate ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <exception cref="KeyNotFoundException">
	/// No request with the given <paramref name="requestId"/> exists, so there is nothing whose completion
	/// could be recorded. This throws rather than returning quietly: a completion is an attestation that
	/// an erasure was carried out, so accepting one for a request that does not exist would record that
	/// data was erased which nobody asked to erase, with nothing anywhere reporting it.
	/// </exception>
	Task RecordCompletionAsync(
		Guid requestId,
		int keysDeleted,
		int recordsAffected,
		Guid certificateId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Stages the generation this request is ABOUT TO destroy at <paramref name="keyHandle"/>, durably, BEFORE
	/// the destruction is attempted.
	/// </summary>
	/// <param name="requestId">The request whose pass is about to destroy the key.</param>
	/// <param name="keyHandle">The key handle whose material is about to be destroyed.</param>
	/// <param name="keyGeneration">
	/// The generation identifier of the material about to be destroyed, read from the key backend while that
	/// material still existed.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <remarks>
	/// <para>
	/// <b>The destruction destroys this write's own input, which is why it has to happen first.</b> On every
	/// backend the framework supports, the generation identifier IS backend material — a key-vault version id,
	/// a CMK id, a sidecar marker, an in-memory entry — and none of them is readable once the material is gone.
	/// So a crash between destroying the key and recording its generation is not a window a retry repairs: the
	/// generation is unreadable forever, the retry cannot land the record either, and the subject's ciphertext
	/// is then permanently undecryptable with no way to report their erasure. Staging first is what leaves the
	/// retry something to work from.
	/// </para>
	/// <para>
	/// <b>A staged row is NOT a destruction record and MUST NOT be read as one.</b> Between this call and the
	/// destruction it names LIVE material, so a predicate that could match it would report a live key as
	/// destroyed — the catastrophic direction. Staged intents and destruction records are therefore held apart,
	/// and <see cref="IKeyDestructionLedger.IsGenerationDestroyedAsync"/> resolves only against the records: a
	/// row's existence in the ledger IS the destruction statement, so there is no column to interpret and no
	/// clause to forget.
	/// </para>
	/// <para>
	/// <b>Idempotent, and appends rather than replaces.</b> Staging a generation already staged for the same
	/// request and handle is a no-op. A later pass adds to what earlier passes staged and never truncates it.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="keyHandle"/> or <paramref name="keyGeneration"/> is null or empty.
	/// </exception>
	/// <exception cref="KeyNotFoundException">
	/// No request with the given <paramref name="requestId"/> exists in this tenant, so there is nothing a
	/// destruction could be staged against.
	/// </exception>
	Task StageKeyDestructionAsync(
		Guid requestId,
		string keyHandle,
		string keyGeneration,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the generations this request staged for destruction at <paramref name="keyHandle"/>.
	/// </summary>
	/// <param name="requestId">The request whose staged intents to read.</param>
	/// <param name="keyHandle">The key handle the intents were staged against.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// The staged generations, in no particular order; empty when nothing was staged for this request and
	/// handle. Never <see langword="null"/>.
	/// </returns>
	/// <remarks>
	/// This is the recovery read, and it is the ONLY member that may name a staged intent. It exists because a
	/// pass that destroyed material and then failed before recording it can no longer read the generation from
	/// the backend — the destruction took it — so the staged row is the only remaining copy. A caller that uses
	/// an answer here to write a destruction record is asserting that the material is gone, and it owes
	/// positive evidence of that: this member reports what was STAGED, which is a statement about an intention,
	/// never about an outcome.
	/// </remarks>
	/// <exception cref="ArgumentException">Thrown when <paramref name="keyHandle"/> is null or empty.</exception>
	Task<IReadOnlyList<string>> GetStagedKeyGenerationsAsync(
		Guid requestId,
		string keyHandle,
		CancellationToken cancellationToken);

	/// <summary>
	/// Records that this request destroyed the generation <paramref name="keyGeneration"/> at
	/// <paramref name="keyHandle"/>, durably, as soon as the destruction returns.
	/// </summary>
	/// <param name="requestId">The request whose pass destroyed the key.</param>
	/// <param name="keyHandle">The key handle whose material is now irrecoverable.</param>
	/// <param name="keyGeneration">
	/// The generation identifier of the destroyed material. This is the ledger's KEY: a generation is minted
	/// once and never reused, so one generation is one destruction and a second row for it is a contradiction
	/// the store refuses. <paramref name="requestId"/> and <paramref name="keyHandle"/> are audit attributes
	/// and are never part of that key.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <remarks>
	/// <para>
	/// <b>This exists because "the key is gone" and "we never held it" are different facts with opposite
	/// consequences, and the key store cannot tell them apart.</b> Asking a provider to delete a key it has
	/// already destroyed reports it as absent — the same answer it gives for a key that never existed. So an
	/// erasure that destroyed a key and then failed part-way through the remaining ones attested that key on
	/// its first pass and, on the retry, attested nothing for it: the subject's data was destroyed and their
	/// erasure could never be reported complete. This record is what lets the retry attest what its own
	/// earlier pass achieved.
	/// </para>
	/// <para>
	/// <b>Written per key, as each destruction returns, not at completion.</b> The pass that needs this record
	/// is the one that did not reach completion, so a write deferred to the end is a write that never happens
	/// in the only case that matters.
	/// </para>
	/// <para>
	/// <b>Idempotent on the GENERATION, and appends rather than replaces.</b> Recording a generation already
	/// recorded is a no-op. Two DIFFERENT generations destroyed at one handle are two destructions and produce
	/// TWO records with their own instants — keying the record on the handle instead would silently drop the
	/// second, which is a destruction that happened and is not on file.
	/// </para>
	/// <para>
	/// A store that cannot persist this cannot certify a retried erasure, and no read of the subject's
	/// ciphertext can ever report their erasure, so this is required rather than an optional capability.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="keyHandle"/> or <paramref name="keyGeneration"/> is null or empty.
	/// </exception>
	Task RecordKeyDestroyedAsync(
		Guid requestId,
		string keyHandle,
		string keyGeneration,
		CancellationToken cancellationToken);

	/// <summary>
	/// Records erasure cancellation.
	/// </summary>
	/// <param name="requestId">The request ID.</param>
	/// <param name="reason">Cancellation reason.</param>
	/// <param name="cancelledBy">Who cancelled.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if cancelled, false if not found or already executed.</returns>
	Task<bool> RecordCancellationAsync(
		Guid requestId,
		string reason,
		string cancelledBy,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets a sub-interface or related service from this store implementation.
	/// </summary>
	/// <param name="serviceType">The type of service to retrieve (e.g. <see cref="IErasureCertificateStore"/>, <see cref="IErasureQueryStore"/>).</param>
	/// <returns>The service instance, or <see langword="null"/> if the store does not implement the requested type.</returns>
	/// <remarks>
	/// This follows the <c>IServiceProvider.GetService</c> escape-hatch pattern from Microsoft design guidelines,
	/// allowing callers to discover optional sub-interfaces without widening the core interface.
	/// </remarks>
	object? GetService(Type serviceType);
}
