// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides GDPR-compliant consent management capabilities.
/// </summary>
/// <remarks>
/// <para>
/// This service manages the recording, querying, and withdrawal of data subject
/// consent per GDPR Article 7. It maintains a complete audit trail of all
/// consent lifecycle events.
/// </para>
/// </remarks>
public interface IConsentService
{
	/// <summary>
	/// Records a consent grant for a data subject and purpose.
	/// </summary>
	/// <param name="record">The consent record to store.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="record"/> is null.</exception>
	Task RecordConsentAsync(
		ConsentRecord record,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current consent status for a data subject and purpose.
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
	/// Withdraws consent for a data subject and purpose.
	/// </summary>
	/// <param name="subjectId">The data subject identifier.</param>
	/// <param name="purpose">The processing purpose.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if consent was withdrawn; false if no consent existed.</returns>
	Task<bool> WithdrawConsentAsync(
		string subjectId,
		string purpose,
		CancellationToken cancellationToken);
}

/// <summary>
/// Represents a consent record for GDPR Article 7 compliance.
/// </summary>
public sealed record ConsentRecord
{
	/// <summary>
	/// Gets the data subject identifier.
	/// </summary>
	public required string SubjectId { get; init; }

	/// <summary>
	/// Gets the processing purpose for which consent is granted.
	/// </summary>
	public required string Purpose { get; init; }

	/// <summary>
	/// Gets the timestamp when consent was granted.
	/// </summary>
	public DateTimeOffset GrantedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the timestamp when consent expires, if applicable.
	/// </summary>
	public DateTimeOffset? ExpiresAt { get; init; }

	/// <summary>
	/// Gets the legal basis for the consent.
	/// </summary>
	public LegalBasis LegalBasis { get; init; } = LegalBasis.Consent;

	/// <summary>
	/// Gets a value indicating whether the consent has been withdrawn.
	/// </summary>
	public bool IsWithdrawn { get; init; }

	/// <summary>
	/// Gets the timestamp when consent was withdrawn, if applicable.
	/// </summary>
	public DateTimeOffset? WithdrawnAt { get; init; }
}

/// <summary>
/// Configuration options for consent management.
/// </summary>
public sealed class ConsentOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether explicit consent is required for all processing purposes.
	/// Default: true (recommended for GDPR compliance).
	/// </summary>
	public bool RequireExplicitConsent { get; set; } = true;

	/// <summary>
	/// Gets or sets the default expiration period in days for consent records.
	/// Zero means consent does not expire automatically.
	/// Default: 365 days.
	/// </summary>
	public int DefaultExpirationDays { get; set; } = 365;
}
