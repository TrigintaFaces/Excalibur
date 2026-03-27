// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.NonHumanIdentity;

/// <summary>
/// Default provider that classifies all principals as <see cref="PrincipalType.Human"/>
/// for backward compatibility with systems that do not distinguish principal types.
/// </summary>
internal sealed class DefaultPrincipalTypeProvider : IPrincipalTypeProvider
{
	/// <inheritdoc/>
	public Task<PrincipalType> GetPrincipalTypeAsync(string principalId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(principalId);
		return Task.FromResult(PrincipalType.Human);
	}

	/// <inheritdoc/>
	public object? GetService(Type serviceType) => null;
}
