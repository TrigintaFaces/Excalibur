// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Compliance;

/// <summary>
/// Provides persistence for compliance records including consent, erasure logs,
/// and subject access request tracking.
/// </summary>
/// <remarks>
/// <para>
/// Implementations should provide durable storage for compliance artifacts
/// required by GDPR and other regulations. Postgres and MongoDB provider
/// packages implement this interface; a host on another store supplies its own
/// implementation.
/// </para>
/// <para>
/// <b>Tenant confinement.</b> Every member here is confined to the ambient tenant established for this
/// store instance: <see cref="GetConsentAsync"/> returns none of another tenant's consent records and
/// resolves the caller's own when it exists, and every write lands in the caller's own partition. Which
/// mechanism a given provider uses to hold that boundary is declared by its capability marker —
/// <see cref="ITenantScopingCapability{TContract}"/> for a store that reads an ambient tenant — and the
/// package's own <c>ARCHITECTURE.md</c> states the falsifiable guarantee and how it is verified. A store
/// presenting no marker is not confined by the framework.
/// </para>
/// </remarks>
[TenantOwned]
public interface IComplianceStore
{
	/// <summary>
	/// Stores a consent record.
	/// </summary>
	/// <param name="record">The consent record to store.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task StoreConsentAsync(
		ConsentRecord record,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current consent status for a subject and purpose.
	/// </summary>
	/// <param name="subjectId">The data subject identifier.</param>
	/// <param name="purpose">The processing purpose.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The consent record, or null if no consent exists.</returns>
	/// <remarks>
	/// Confined to the ambient tenant established for this store instance: a consent record stored under
	/// another tenant for the same <paramref name="subjectId"/> and <paramref name="purpose"/> is reported
	/// as not found, the same as one that was never recorded.
	/// </remarks>
	Task<ConsentRecord?> GetConsentAsync(
		string subjectId,
		string purpose,
		CancellationToken cancellationToken);

	/// <summary>
	/// Stores an erasure log entry for audit purposes.
	/// </summary>
	/// <param name="subjectId">The data subject identifier.</param>
	/// <param name="details">Details of the erasure operation.</param>
	/// <param name="erasedAt">The timestamp of the erasure.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task StoreErasureLogAsync(
		string subjectId,
		string details,
		DateTimeOffset erasedAt,
		CancellationToken cancellationToken);

	/// <summary>
	/// Stores a subject access request for tracking.
	/// </summary>
	/// <param name="result">The subject access result to store.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task StoreSubjectAccessRequestAsync(
		SubjectAccessResult result,
		CancellationToken cancellationToken);
}
