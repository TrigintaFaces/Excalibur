// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Pseudonymizes data-subject identifiers (the lookup key for GDPR erasure, legal-hold, and data-inventory
/// records) into a stable, non-reversible token.
/// </summary>
/// <remarks>
/// All GDPR erasure, legal-hold, and data-inventory components MUST use the same registered hasher so a
/// given identifier maps to the same token across services and stores. Implementations use a keyed
/// construction (a secret pepper held outside the record store) so the token is not reversible by
/// rainbow-table / dictionary attack against low-entropy identifiers such as e-mail addresses.
/// </remarks>
public interface IDataSubjectHasher
{
	/// <summary>
	/// Computes the stable pseudonymization token for a data-subject identifier.
	/// </summary>
	/// <param name="dataSubjectId">The raw data-subject identifier (e-mail, username, national ID, …).</param>
	/// <returns>An uppercase hex-encoded keyed hash of the identifier.</returns>
	string HashDataSubjectId(string dataSubjectId);
}
