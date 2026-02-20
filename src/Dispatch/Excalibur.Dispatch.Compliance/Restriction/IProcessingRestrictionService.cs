// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance.Restriction;

/// <summary>
/// Provides GDPR Article 18 processing restriction capabilities.
/// </summary>
/// <remarks>
/// <para>
/// Article 18 of the GDPR grants data subjects the right to restrict processing
/// of their personal data under specific circumstances. This interface provides
/// the operations needed to manage processing restrictions.
/// </para>
/// <para>
/// When processing is restricted, the data may be stored but not actively processed
/// until the restriction is lifted or the data subject consents to further processing.
/// </para>
/// <para>
/// Follows the <c>Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck</c> pattern
/// with a minimal interface surface (3 methods).
/// </para>
/// </remarks>
public interface IProcessingRestrictionService
{
	/// <summary>
	/// Restricts processing of a data subject's personal data.
	/// </summary>
	/// <param name="subjectId">The data subject identifier.</param>
	/// <param name="reason">The reason for restricting processing.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="subjectId"/> is null or empty.
	/// </exception>
	Task RestrictAsync(string subjectId, RestrictionReason reason, CancellationToken cancellationToken);

	/// <summary>
	/// Removes the processing restriction for a data subject.
	/// </summary>
	/// <param name="subjectId">The data subject identifier.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="subjectId"/> is null or empty.
	/// </exception>
	Task UnrestrictAsync(string subjectId, CancellationToken cancellationToken);

	/// <summary>
	/// Checks whether processing is restricted for a data subject.
	/// </summary>
	/// <param name="subjectId">The data subject identifier.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// <see langword="true"/> if processing is restricted for the specified subject;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="subjectId"/> is null or empty.
	/// </exception>
	Task<bool> IsRestrictedAsync(string subjectId, CancellationToken cancellationToken);
}
