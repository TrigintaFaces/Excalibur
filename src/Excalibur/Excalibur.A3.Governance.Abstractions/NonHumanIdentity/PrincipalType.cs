// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Governance.NonHumanIdentity;

/// <summary>
/// Classifies the type of principal (identity) for governance purposes.
/// </summary>
/// <remarks>
/// <para>
/// Used to differentiate between human and non-human identities when applying
/// governance policies such as access reviews, provisioning workflows, and
/// API key management.
/// </para>
/// <para>
/// Consumers can extend classification beyond these built-in values by using
/// <see cref="IPrincipalTypeProvider.GetService(Type)"/> to access custom
/// classification logic.
/// </para>
/// </remarks>
public enum PrincipalType
{
	/// <summary>
	/// A human user identity.
	/// </summary>
	Human = 0,

	/// <summary>
	/// A service account used for automated processes.
	/// </summary>
	ServiceAccount = 1,

	/// <summary>
	/// A bot or automated agent identity.
	/// </summary>
	Bot = 2,

	/// <summary>
	/// An API key-based identity.
	/// </summary>
	ApiKey = 3,
}
