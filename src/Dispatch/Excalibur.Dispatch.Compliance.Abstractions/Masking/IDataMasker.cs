// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Service for masking sensitive data (PII/PHI) in strings and objects.
/// </summary>
/// <remarks>
/// <para>
/// The data masker uses regex-based pattern matching to identify
/// and mask sensitive data patterns including:
/// </para>
/// <list type="bullet">
/// <item><description>Email addresses</description></item>
/// <item><description>Phone numbers</description></item>
/// <item><description>Social Security Numbers (SSN)</description></item>
/// <item><description>Credit card numbers (PCI-DSS compliance)</description></item>
/// <item><description>IP addresses</description></item>
/// </list>
/// <para>
/// For object masking, the service uses reflection to discover properties marked with
/// <see cref="Classification.PersonalDataAttribute"/> or <see cref="Classification.SensitiveAttribute"/>
/// with <c>MaskInLogs = true</c>.
/// </para>
/// </remarks>
public interface IDataMasker
{
	/// <summary>
	/// Masks sensitive data patterns in a string.
	/// </summary>
	/// <param name="input">The input string to mask.</param>
	/// <param name="rules">The masking rules to apply.</param>
	/// <returns>The masked string with sensitive data replaced.</returns>
	string Mask(string input, MaskingRules rules);

	/// <summary>
	/// Masks sensitive properties in an object based on attributes and rules.
	/// </summary>
	/// <typeparam name="T">The type of object to mask.</typeparam>
	/// <param name="obj">The object to mask.</param>
	/// <returns>A copy of the object with sensitive data masked.</returns>
	/// <remarks>
	/// Properties marked with [PersonalData(MaskInLogs = true)] or [Sensitive(MaskInLogs = true)]
	/// will have their values masked. The original object is not modified.
	/// </remarks>
	[RequiresUnreferencedCode("Object masking uses JSON serialization which may require preserved members for the runtime type.")]
	[RequiresDynamicCode("Object masking uses JSON serialization which may require dynamic code generation.")]
	T MaskObject<T>(T obj) where T : class;

	/// <summary>
	/// Masks sensitive data patterns in a string using default rules.
	/// </summary>
	/// <param name="input">The input string to mask.</param>
	/// <returns>The masked string with all default patterns masked.</returns>
	string MaskAll(string input);
}

/// <summary>
/// Configures which data patterns to mask.
/// </summary>
public sealed class MaskingRules
{
	/// <summary>
	/// Gets default masking rules with all common patterns enabled.
	/// </summary>
	public static MaskingRules Default => new();

	/// <summary>
	/// Gets strict masking rules for maximum protection (all patterns masked).
	/// </summary>
	public static MaskingRules Strict => new()
	{
		MaskEmail = true,
		MaskPhone = true,
		MaskSsn = true,
		MaskCardNumber = true,
		MaskIpAddress = true,
		MaskDateOfBirth = true
	};

	/// <summary>
	/// Gets PCI-DSS compliant rules (card numbers only).
	/// </summary>
	public static MaskingRules PciDss => new()
	{
		MaskEmail = false,
		MaskPhone = false,
		MaskSsn = false,
		MaskCardNumber = true,
		MaskIpAddress = false,
		MaskDateOfBirth = false
	};

	/// <summary>
	/// Gets HIPAA compliant rules (PHI patterns).
	/// </summary>
	public static MaskingRules Hipaa => new()
	{
		MaskEmail = true,
		MaskPhone = true,
		MaskSsn = true,
		MaskCardNumber = false,
		MaskIpAddress = true,
		MaskDateOfBirth = true
	};

	/// <summary>
	/// Gets or sets a value indicating whether email addresses should be masked.
	/// </summary>
	/// <value>True to mask emails (john@example.com -> j***@e***.com), false to leave unmasked.</value>
	public bool MaskEmail { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether phone numbers should be masked.
	/// </summary>
	/// <value>True to mask phone numbers (555-123-4567 -> ***-***-4567), false to leave unmasked.</value>
	public bool MaskPhone { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether Social Security Numbers should be masked.
	/// </summary>
	/// <value>True to mask SSNs (123-45-6789 -> ***-**-6789), false to leave unmasked.</value>
	public bool MaskSsn { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether credit card numbers should be masked.
	/// </summary>
	/// <value>True to mask card numbers (4111-1111-1111-1111 -> ****-****-****-1111), false to leave unmasked.</value>
	public bool MaskCardNumber { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether IP addresses should be masked.
	/// </summary>
	/// <value>True to mask IPs (192.168.1.100 -> ***.***.***.100), false to leave unmasked.</value>
	public bool MaskIpAddress { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether dates of birth should be masked.
	/// </summary>
	/// <value>True to mask DOBs, false to leave unmasked.</value>
	public bool MaskDateOfBirth { get; set; }

	/// <summary>
	/// Gets or sets the character used for masking. Defaults to '*'.
	/// </summary>
	public char MaskCharacter { get; set; } = '*';
}
