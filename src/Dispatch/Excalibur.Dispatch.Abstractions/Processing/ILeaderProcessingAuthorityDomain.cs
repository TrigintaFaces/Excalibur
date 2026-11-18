// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch;

/// <summary>
/// Optionally identifies the shared authority behind different leader-processing gate wrappers.
/// </summary>
/// <remarks>
/// Identity is compared by reference, not equality. Return the same nonnull object for the lifetime of
/// one authority's monotonic token sequence. Never reuse it for unrelated elections or reset the token
/// sequence while retaining it. This identity neither authorizes work nor mints a token. The framework
/// does not dispose it. Gates without this capability use their own object reference as their domain.
/// </remarks>
public interface ILeaderProcessingAuthorityDomain
{
	/// <summary>Gets the stable, nonnull authority identity shared by these wrappers.</summary>
	object AuthorityDomain { get; }
}
