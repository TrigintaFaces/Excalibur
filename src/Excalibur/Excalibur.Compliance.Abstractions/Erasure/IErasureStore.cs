// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


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
/// a caller is expected to branch on. <see cref="RecordCompletionAsync"/> throws instead, because
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
	/// <param name="errorMessage">Optional error message if failed.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if updated, false if not found.</returns>
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
