// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Compliance;

/// <summary>
/// Marks a property for field-level encryption at rest.
/// </summary>
/// <remarks>
/// <para>
/// Properties decorated with this attribute are encrypted on write and decrypted on read by the
/// field-encryption paths, including <c>EncryptingProjectionStoreDecorator</c>.
/// </para>
/// <para>
/// <see cref="string"/> and <see cref="byte"/>[] properties are supported. A <see cref="string"/>
/// property holds <see cref="EncryptedFieldBinding.StringEnvelopePrefix"/> followed by the
/// Base64-encoded envelope, so a stored value is not itself valid Base64; the marker is outside
/// the Base64 alphabet precisely so an encrypted value cannot be confused with a plaintext one
/// that happens to decode. A <see cref="byte"/>[] property holds the raw envelope. Both are
/// restored to the original value on read, so the encoding is not something a consumer has to
/// handle.
/// </para>
/// <para>
/// A property must have both a getter and a setter, because encryption replaces the value in place.
/// Annotating a property that cannot be encrypted — an unsupported type, or a missing accessor — is
/// rejected when the type is first used rather than ignored, so an annotation never silently leaves
/// data unprotected.
/// </para>
/// <para>
/// This attribute provides encryption at rest under a key purpose of its own, independent of any data
/// subject. <c>[Sensitive]</c> also selects a property for encryption, but it states no key purpose, so
/// a property carrying only <c>[Sensitive]</c> is encrypted under the configured default purpose; it
/// additionally redacts the value in logs and output, and those two protections are independent of each
/// other. <c>[PersonalData]</c> encrypts under a different key again: on a record that also carries
/// <c>[DataSubjectId]</c>, with crypto-shredding registered, it selects the property for encryption
/// under that subject's key, so erasing the subject destroys the value.
/// </para>
/// <para>
/// <strong>Say what the value is, as well as that it is encrypted.</strong> This attribute states a key
/// purpose and nothing about classification, so on its own it leaves open the one question erasure has
/// to answer: is this personal data belonging to a data subject, or is it not? Pair it with
/// <c>[Sensitive]</c> for a credential, a secret or any other value that belongs to the system rather
/// than to a person, and with <c>[PersonalData]</c> for a value that belongs to a data subject. Both
/// pairings encrypt the value exactly once — <c>[Sensitive]</c> states no key purpose, so it never
/// contradicts the purpose named here.
/// </para>
/// <para>
/// The reason the classification matters is that encryption is what makes a value <em>opaque</em>:
/// afterwards the framework is the only party that can tell what the property held. A value classified
/// as personal data is the erasure inventory's business, and a credential is not — it belongs to no data
/// subject, so listing it in a GDPR erasure inventory would be wrong rather than merely redundant. An
/// unclassified encrypted property cannot be placed on either side of that line.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class PaymentGatewaySettings
/// {
///     public string Id { get; set; }
///     public string DisplayName { get; set; }
///
///     // Secrets belong to the SYSTEM, not to a data subject. [Sensitive] says so, and is what
///     // keeps them out of a GDPR erasure inventory, where a record with no data subject does
///     // not belong. [EncryptedField] names the key purpose; [Sensitive] names what the value is.
///     [EncryptedField(Purpose = "payment-gateway-credentials")]
///     [Sensitive(Classification = DataClassification.Restricted)]
///     public string ApiKey { get; set; }
///
///     [EncryptedField(Purpose = "payment-gateway-credentials")]
///     [Sensitive(Classification = DataClassification.Restricted)]
///     public byte[] WebhookSigningSecret { get; set; }
/// }
///
/// // Personal data tied to a subject is NOT this attribute's job. Annotate it with
/// // [PersonalData] on a record carrying [DataSubjectId], so that erasing the subject
/// // destroys the key that read it.
/// public record Customer
/// {
///     [DataSubjectId]
///     public string CustomerId { get; init; }
///
///     [PersonalData]
///     public string SocialSecurityNumber { get; init; }
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
