// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance.Rectification;

/// <summary>
/// Provides GDPR Article 16 data rectification capabilities.
/// </summary>
/// <remarks>
/// <para>
/// Article 16 of the GDPR grants data subjects the right to obtain the
/// rectification of inaccurate personal data without undue delay. This
/// interface provides operations for rectifying personal data and
/// querying the rectification history for audit purposes.
/// </para>
/// <para>
/// Follows the <c>Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck</c> pattern
/// with a minimal interface surface (2 methods).
/// </para>
/// </remarks>
public interface IRectificationService
{
	/// <summary>
	/// Rectifies a data subject's personal data by updating a specific field.
	/// </summary>
	/// <param name="request">The rectification request containing the field and values to update.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="request"/> is null.
	/// </exception>
	Task RectifyAsync(RectificationRequest request, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the rectification history for a data subject.
	/// </summary>
	/// <param name="subjectId">The data subject identifier.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// A read-only list of rectification records for the specified subject,
	/// ordered chronologically, or an empty list if none found.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="subjectId"/> is null or empty.
	/// </exception>
	Task<IReadOnlyList<RectificationRecord>> GetRectificationHistoryAsync(
		string subjectId,
		CancellationToken cancellationToken);
}
