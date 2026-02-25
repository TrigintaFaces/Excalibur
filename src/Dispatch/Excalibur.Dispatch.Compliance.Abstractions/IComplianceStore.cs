// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides persistence for compliance records including consent, erasure logs,
/// and subject access request tracking.
/// </summary>
/// <remarks>
/// <para>
/// Implementations should provide durable storage for compliance artifacts
/// required by GDPR and other regulations. Provider-specific implementations
/// (SQL Server, Postgres, MongoDB) implement this interface.
/// </para>
/// </remarks>
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
