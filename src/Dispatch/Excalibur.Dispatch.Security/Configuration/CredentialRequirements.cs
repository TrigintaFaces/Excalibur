// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Represents credential security requirements.
/// </summary>
public sealed class CredentialRequirements
{
	/// <summary>
	/// Gets or sets the minimum length required for credentials.
	/// </summary>
	/// <value> The minimum length required for credentials. The default is 12. </value>
	public int MinimumLength { get; set; } = 12;

	/// <summary>
	/// Gets or sets a value indicating whether credentials must contain at least one uppercase letter.
	/// </summary>
	/// <value>
	/// <see langword="true" /> if credentials must contain at least one uppercase letter; otherwise, <see langword="false" />. The default
	/// is <see langword="true" />.
	/// </value>
	public bool RequireUppercase { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether credentials must contain at least one lowercase letter.
	/// </summary>
	/// <value>
	/// <see langword="true" /> if credentials must contain at least one lowercase letter; otherwise, <see langword="false" />. The default
	/// is <see langword="true" />.
	/// </value>
	public bool RequireLowercase { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether credentials must contain at least one digit.
	/// </summary>
	/// <value>
	/// <see langword="true" /> if credentials must contain at least one digit; otherwise, <see langword="false" />. The default is <see langword="true" />.
	/// </value>
	public bool RequireDigit { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether credentials must contain at least one special character.
	/// </summary>
	/// <value>
	/// <see langword="true" /> if credentials must contain at least one special character; otherwise, <see langword="false" />. The default
	/// is <see langword="true" />.
	/// </value>
	public bool RequireSpecialCharacter { get; set; } = true;

	/// <summary>
	/// Gets or initializes a set of prohibited values that credentials cannot use.
	/// </summary>
	/// <value> A set of prohibited values that credentials cannot use, or <see langword="null" /> if no prohibited values are specified. </value>
	public ISet<string>? ProhibitedValues { get; init; }
}
