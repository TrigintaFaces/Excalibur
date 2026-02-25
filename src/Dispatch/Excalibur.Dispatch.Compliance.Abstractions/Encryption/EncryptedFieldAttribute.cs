// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Marks a byte array property for field-level encryption.
/// </summary>
/// <remarks>
/// <para>
/// Properties decorated with this attribute will be automatically encrypted/decrypted
/// by store decorators like <c>EncryptingProjectionStoreDecorator</c>.
/// </para>
/// <para>
/// Only <see cref="byte"/>[] properties can be marked with this attribute. For string properties,
/// serialize to byte[] first, then encrypt.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class CustomerProjection
/// {
///     public string Id { get; set; }
///     public string Name { get; set; }
///
///     [EncryptedField]
///     public byte[] SocialSecurityNumber { get; set; }
///
///     [EncryptedField]
///     public byte[] CreditCardData { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class EncryptedFieldAttribute : Attribute
{
	/// <summary>
	/// Gets or sets the encryption purpose for key selection.
	/// </summary>
	/// <remarks>
	/// When set, overrides the default purpose from <c>EncryptionOptions.DefaultPurpose</c>.
	/// Use this to select different keys for different data classifications.
	/// </remarks>
	public string? Purpose { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this field requires FIPS 140-2 compliance.
	/// </summary>
	/// <remarks>
	/// When <c>true</c>, encryption operations will fail if the provider is not FIPS compliant.
	/// </remarks>
	public bool RequireFipsCompliance { get; set; }
}
