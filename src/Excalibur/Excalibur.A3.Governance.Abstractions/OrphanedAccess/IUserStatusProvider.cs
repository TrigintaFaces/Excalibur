// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.OrphanedAccess;

/// <summary>
/// Provides the current status of a principal in the identity system.
/// </summary>
/// <remarks>
/// <para>
/// Consumers must implement this interface to connect the orphaned access detector
/// to their identity provider (Active Directory, Entra ID, SCIM, HR system, etc.).
/// No default implementation is provided.
/// </para>
/// </remarks>
public interface IUserStatusProvider
{
	/// <summary>
	/// Gets the current status of the specified principal, including when the status last changed.
	/// </summary>
	/// <param name="principalId">The unique identifier of the principal.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A <see cref="PrincipalStatusResult"/> containing the status and optional change timestamp.</returns>
	Task<PrincipalStatusResult> GetStatusAsync(string principalId, CancellationToken cancellationToken);
}
