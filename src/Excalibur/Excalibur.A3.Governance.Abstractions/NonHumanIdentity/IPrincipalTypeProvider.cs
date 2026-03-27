// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.NonHumanIdentity;

/// <summary>
/// Resolves the <see cref="PrincipalType"/> for a given principal identifier.
/// </summary>
/// <remarks>
/// <para>
/// Consumers implement this interface to integrate with their identity system
/// (e.g., Azure AD, LDAP, custom user store) for principal classification.
/// The framework provides a default implementation that returns
/// <see cref="PrincipalType.Human"/> for all principals (backward compatibility).
/// </para>
/// <para>
/// For extended classification beyond the built-in <see cref="PrincipalType"/> values,
/// use <see cref="GetService"/> to access custom classification services.
/// </para>
/// </remarks>
public interface IPrincipalTypeProvider
{
	/// <summary>
	/// Determines the principal type for the specified principal.
	/// </summary>
	/// <param name="principalId">The principal identifier to classify.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The <see cref="PrincipalType"/> classification for the principal.</returns>
	Task<PrincipalType> GetPrincipalTypeAsync(string principalId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets a sub-interface or service from this provider for extended classification.
	/// </summary>
	/// <param name="serviceType">The type of service to retrieve.</param>
	/// <returns>The service instance, or <see langword="null"/> if not supported.</returns>
	object? GetService(Type serviceType);
}
