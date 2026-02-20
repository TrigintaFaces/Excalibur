// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.A3.Authentication;

/// <summary>
/// Provides constants for authentication claim types.
/// </summary>
public static class AuthClaims
{
	/// <summary>
	/// Represents the email claim type.
	/// </summary>
	public static readonly string Email = nameof(Email).ToUpperInvariant();

	/// <summary>
	/// Represents the family name claim type.
	/// </summary>
	[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Proper claim syntax")]
	public static readonly string FamilyName = nameof(FamilyName).ToUpperInvariant();

	/// <summary>
	/// Represents the given name claim type.
	/// </summary>
	[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Proper claim syntax")]
	public static readonly string GivenName = nameof(GivenName).ToUpperInvariant();

	/// <summary>
	/// Represents the name claim type.
	/// </summary>
	public static readonly string Name = nameof(Name).ToUpperInvariant();

	/// <summary>
	/// Represents the UPN (User Principal Name) claim type.
	/// </summary>
	public static readonly string Upn = nameof(Upn).ToUpperInvariant();
}
