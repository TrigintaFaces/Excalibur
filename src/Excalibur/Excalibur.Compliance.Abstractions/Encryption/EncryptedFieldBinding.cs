// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

namespace Excalibur.Compliance;

/// <summary>
/// The single owner of non-subject field selection — what <see cref="EncryptedFieldAttribute"/> and
/// <see cref="SensitiveAttribute"/> select — and of how an annotated property value crosses between its
/// declared type and the raw bytes an encryptor consumes.
/// </summary>
/// <remarks>
/// <para>
/// <strong>It is not the only selection authority in the framework, and reading it as one has produced a
/// wrong conclusion.</strong> Subject-keyed encryption selects <see cref="PersonalDataAttribute"/>
/// properties separately, because those are bound to a per-subject key that erasure destroys. This type
/// owns the paths where the key is not derived from a data subject; the two authorities are disjoint by
/// annotation and must stay so.
/// </para>
/// <para>
/// Every field-level encryption path routes property selection and value access through this type. The
/// selection predicate previously existed as three byte-identical copies that each filtered
/// <c>PropertyType == typeof(byte[])</c>, so an annotation on a <see cref="string"/> property was dropped
/// silently and its value persisted as plaintext. Concentrating both halves here means a path cannot honour
/// a different set of properties than its siblings, and cannot reintroduce a silent skip.
/// </para>
/// <para>
/// A <see cref="byte"/>[] property holds the envelope verbatim, identified by
/// <see cref="EncryptedData.MagicBytes"/>. A <see cref="string"/> property holds
/// <see cref="StringEnvelopePrefix"/> followed by the Base64 of the same envelope, and is identified by that
/// literal prefix alone, never by decoding. Decoding is not a sound test: an ordinary string can Base64-decode
/// to bytes beginning with the magic, and a decode-first test then reads that plaintext as already-encrypted
/// and stores it in the clear. The prefix ends in a colon, which is outside the Base64 alphabet, so no encoder
/// output can accidentally wear it.
/// </para>
/// </remarks>
public static class EncryptedFieldBinding
{
	/// <summary>The property types that can carry an encrypted value, named once.</summary>
	public const string SupportedTypeNames = "string, byte[]";

	/// <summary>
	/// The marker that begins a <see cref="string"/> property encrypted value. Versioned, so a later envelope
	/// format can be told apart from this one rather than guessed at.
	/// </summary>
	public const string StringEnvelopePrefix = "EXCR1:";

	/// <summary>
	/// The largest encoded envelope, in characters, that a <see cref="string"/> property is decoded from.
	/// </summary>
	/// <remarks>
	/// Decoding is what a hostile length costs, so the length is checked before the buffer is allocated
	/// rather than after. A stored value is consumer-writable, and the decode path allocates the encoded
	/// text, the decode buffer, and then the JSON reader's own view of the result, so an unbounded value
	/// multiplies into large-object-heap pressure several times over. A field-level ciphertext does not
	/// legitimately approach this size.
	/// </remarks>
	public const int MaxEncodedEnvelopeLength = 1024 * 1024;

	/// <summary>
	/// Gets a value indicating whether <paramref name="propertyType"/> can carry an encrypted value.
	/// </summary>
	/// <param name="propertyType">The declared type of the annotated property.</param>
	/// <returns><see langword="true"/> when the type is supported.</returns>
	public static bool IsSupported(Type propertyType) =>
		propertyType == typeof(string) || propertyType == typeof(byte[]);

	/// <summary>
	/// Selects the <see cref="EncryptedFieldAttribute"/>-annotated properties of <typeparamref name="T"/>,
	/// rejecting any annotation that cannot be honoured instead of skipping it.
	/// </summary>
	/// <typeparam name="T">The annotated type.</typeparam>
	/// <returns>The annotated properties, in declaration order.</returns>
	/// <exception cref="EncryptionException">
	/// An annotated property is of an unsupported type, or is missing a getter or a setter. Such an
	/// annotation has never encrypted anything, so rejecting it withdraws no working behaviour: it discloses,
	/// at construction time, protection the caller did not have.
	/// </exception>
	public static PropertyInfo[] Select<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>() =>
		Select(typeof(T));

	/// <inheritdoc cref="Select{T}()"/>
	/// <param name="type">The annotated type.</param>
	public static PropertyInfo[] Select(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
	{
		var selected = Inspect(type, out var unhonourable);
		return unhonourable.Count > 0 ? throw new EncryptionException(unhonourable[0]) : selected;
	}

	/// <summary>
	/// Selects the annotated properties that CAN be honoured, and reports the ones that cannot, without
	/// throwing.
	/// </summary>
	/// <param name="type">The annotated type.</param>
	/// <param name="unhonourable">
	/// One message per annotated property that cannot be encrypted, explaining why. Empty when every
	/// annotation on the type can be honoured.
	/// </param>
	/// <returns>The annotated properties that can be encrypted, in declaration order.</returns>
	/// <remarks>
	/// This is the question a caller asks when it is REPORTING on a type rather than encrypting it — an
	/// estimate, a diagnostic, an inventory. Such a caller wants the count it can honour plus the reasons
	/// for the rest; refusing outright would deny it the answer it exists to produce, and a type carrying
	/// one good annotation and one bad one would estimate as zero fields rather than one field and a
	/// warning. A caller that is about to ENCRYPT asks <see cref="Select(Type)"/>, which refuses, because
	/// proceeding there would silently leave a field unprotected.
	/// </remarks>
	public static PropertyInfo[] Inspect(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type,
		out IReadOnlyList<string> unhonourable)
	{
		ArgumentNullException.ThrowIfNull(type);

		var selected = new List<PropertyInfo>();
		var problems = new List<string>();
		foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			var encryptedField = property.GetCustomAttribute<EncryptedFieldAttribute>();
			var sensitive = property.GetCustomAttribute<SensitiveAttribute>();

			// Selection is a UNION. [Sensitive] names data that must not be stored in the clear -- trade
			// secrets, API keys, credentials -- so it selects for encryption exactly as [EncryptedField]
			// does. It states no key purpose of its own, so it can never contradict the annotation beside
			// it: the purpose comes from whichever attribute states one, and otherwise from the configured
			// default.
			if (encryptedField is null && sensitive is null)
			{
				continue;
			}

			// The marker names the attribute the author actually wrote, so a diagnostic points at the line
			// they need to change rather than at an annotation they never applied.
			var marker = encryptedField is not null ? "[EncryptedField]" : "[Sensitive]";

			// A property erased on request cannot also be held under a key chosen to survive that erasure.
			// Neither annotation may silently win, so the contradiction is reported rather than resolved.
			// [Sensitive] is absent from this test by construction: it states no purpose, so it cannot
			// disagree with the per-subject key.
			if (property.GetCustomAttribute<PersonalDataAttribute>() is not null
				&& !string.IsNullOrWhiteSpace(encryptedField?.Purpose))
			{
				problems.Add(
					$"{type.FullName}.{property.Name} carries both [PersonalData] and [EncryptedField] with " +
					$"Purpose '{encryptedField!.Purpose}'. [PersonalData] binds the value to a per-subject key " +
					"that is destroyed when the subject is erased; a stated purpose binds it to a key that " +
					"survives erasure. Honouring either one silently would make the other's guarantee false. " +
					"Remove the explicit Purpose to keep the value erasable, or remove [PersonalData] if the " +
					"value must outlive the subject.");
				continue;
			}

			if (!IsSupported(property.PropertyType))
			{
				problems.Add(
					$"{marker} on {type.FullName}.{property.Name} cannot be honoured: its type " +
					$"{property.PropertyType.FullName} is not encryptable. Supported types are {SupportedTypeNames}. " +
					"Remove the annotation, or change the property to a supported type. This property was " +
					"previously skipped in silence and its value stored unencrypted.");
				continue;
			}

			if (!property.CanRead || !property.CanWrite)
			{
				problems.Add(
					$"{marker} on {type.FullName}.{property.Name} cannot be honoured: a getter and a " +
					"setter are both required to replace the value with its ciphertext. This property was " +
					"previously skipped in silence and its value stored unencrypted.");
				continue;
			}

			selected.Add(property);
		}

		unhonourable = problems;
		return [.. selected];
	}

	/// <summary>Reads an annotated property plaintext as the bytes an encryptor consumes.</summary>
	/// <param name="property">The annotated property.</param>
	/// <param name="instance">The instance carrying the value.</param>
	/// <returns>The plaintext bytes, or <see langword="null"/> when the property is empty.</returns>
	public static byte[]? ReadPlaintext(PropertyInfo property, object instance)
	{
		ArgumentNullException.ThrowIfNull(property);

		return property.GetValue(instance) switch
		{
			null => null,
			string s => s.Length == 0 ? null : Encoding.UTF8.GetBytes(s),
			byte[] b => b.Length == 0 ? null : b,
			var other => throw UnreachableType(property, other),
		};
	}

	/// <summary>Writes decrypted plaintext back in the declared representation of the property.</summary>
	/// <param name="property">The annotated property.</param>
	/// <param name="instance">The instance carrying the value.</param>
	/// <param name="plaintext">The decrypted bytes, or <see langword="null"/> to clear the property.</param>
	public static void WritePlaintext(PropertyInfo property, object instance, byte[]? plaintext)
	{
		ArgumentNullException.ThrowIfNull(property);

		property.SetValue(
			instance,
			property.PropertyType == typeof(string)
				? plaintext is null ? null : Encoding.UTF8.GetString(plaintext)
				: plaintext);
	}

	/// <summary>
	/// Gets a value indicating whether an annotated property value is <em>marked</em> as encrypted, without
	/// validating that the envelope is readable.
	/// </summary>
	/// <param name="property">The annotated property.</param>
	/// <param name="instance">The instance carrying the value.</param>
	/// <returns><see langword="true"/> when the value carries the encrypted marker.</returns>
	/// <remarks>
	/// This is the question a <em>writer</em> asks, and it is deliberately not
	/// <see cref="TryReadEnvelope"/>. A writer only needs to know whether encrypting again would double-wrap
	/// a value; it has no use for the envelope contents, and it must not refuse to store a record because the
	/// value already in the field is damaged. Validating here would make a row whose stored ciphertext was
	/// truncated permanently unwritable — and truncation is a <em>write-side</em> cause, since Base64 inflates
	/// by four thirds and <see cref="StringEnvelopePrefix"/> adds six characters, so a column sized for the
	/// plaintext is exactly where it happens. A reader, which cannot return a value it failed to decrypt,
	/// asks <see cref="TryReadEnvelope"/> instead.
	/// </remarks>
	public static bool IsMarkedEncrypted(PropertyInfo property, object instance)
	{
		ArgumentNullException.ThrowIfNull(property);

		return property.GetValue(instance) switch
		{
			byte[] b => EncryptedData.IsFieldEncrypted(b),
			string s => s.StartsWith(StringEnvelopePrefix, StringComparison.Ordinal),
			_ => false,
		};
	}

	/// <summary>
	/// Determines whether an annotated property currently holds an encrypted envelope, and if so yields its
	/// raw bytes.
	/// </summary>
	/// <param name="property">The annotated property.</param>
	/// <param name="instance">The instance carrying the value.</param>
	/// <param name="envelope">The envelope bytes when the method returns <see langword="true"/>.</param>
	/// <returns>
	/// <see langword="true"/> when the property holds an envelope. <see langword="false"/> when it is empty
	/// or holds plaintext.
	/// </returns>
	/// <exception cref="EncryptionException">
	/// The value is marked as an envelope but is not one: a <see cref="string"/> carrying
	/// <see cref="StringEnvelopePrefix"/> whose remainder does not decode, which is how a truncated or
	/// corrupted ciphertext presents. This is thrown rather than reported as "not an envelope", because
	/// answering <see langword="false"/> would let a decrypting caller treat ciphertext as plaintext and hand
	/// it back as though it had been decrypted.
	/// </exception>
	public static bool TryReadEnvelope(
		PropertyInfo property,
		object instance,
		[NotNullWhen(true)] out byte[]? envelope)
	{
		ArgumentNullException.ThrowIfNull(property);

		envelope = null;
		switch (property.GetValue(instance))
		{
			case null:
				return false;

			case byte[] b:
				if (!EncryptedData.IsFieldEncrypted(b))
				{
					return false;
				}

				envelope = b;
				return true;

			case string s:
			{
				if (!s.StartsWith(StringEnvelopePrefix, StringComparison.Ordinal))
				{
					// No marker, so ordinary plaintext. The value is never decoded, which is what stops a
					// plaintext that happens to be Base64 of magic-prefixed bytes reading as an envelope.
					return false;
				}

				var encoded = s[StringEnvelopePrefix.Length..];
				if (encoded.Length > MaxEncodedEnvelopeLength)
				{
					throw new EncryptionException(
						$"[EncryptedField] property {property.DeclaringType?.FullName}.{property.Name} carries " +
						$"{encoded.Length} encoded characters, beyond the {MaxEncodedEnvelopeLength} a field " +
						"envelope may occupy. The value is rejected without decoding it, because decoding is " +
						"what the size would cost. A field-level ciphertext does not legitimately reach this " +
						"size; a value that does is malformed or hostile.");
				}

				// Exact, not an estimate: for a valid Base64 length (a multiple of four) len/4*3 is the
				// largest possible decoded size, and whitespace only ever decreases it. Dividing before
				// multiplying also keeps the product inside int for any string length the runtime permits.
				// The size being provably sufficient is what lets the failure below be reported as
				// corruption — an undersized buffer would produce the identical symptom from TryFromBase64String.
				var buffer = new byte[(encoded.Length / 4 * 3) + 3];
				if (!Convert.TryFromBase64String(encoded, buffer, out var written)
					|| !EncryptedData.IsFieldEncrypted(buffer.AsSpan(0, written)))
				{
					throw new EncryptionException(
						$"[EncryptedField] property {property.DeclaringType?.FullName}.{property.Name} is " +
						"marked as encrypted but its value is not a readable envelope. A value written by " +
						"this framework always decodes; one that does not is truncated or corrupt, most often " +
						"a column too narrow for the ciphertext. It has NOT been treated as plaintext, because " +
						"doing so would return ciphertext to the caller as though it had been decrypted.");
				}

				envelope = buffer[..written];
				return true;
			}

			case var other:
				throw UnreachableType(property, other);
		}
	}

	/// <summary>Writes a framed envelope in the declared representation of the property.</summary>
	/// <param name="property">The annotated property.</param>
	/// <param name="instance">The instance carrying the value.</param>
	/// <param name="framed">The framed envelope bytes.</param>
	public static void WriteEnvelope(PropertyInfo property, object instance, byte[] framed)
	{
		ArgumentNullException.ThrowIfNull(property);
		ArgumentNullException.ThrowIfNull(framed);

		property.SetValue(
			instance,
			property.PropertyType == typeof(string)
				? StringEnvelopePrefix + Convert.ToBase64String(framed)
				: framed);
	}

	/// <summary>
	/// A value whose runtime type is neither <see cref="string"/> nor <see cref="byte"/>[] cannot reach these
	/// accessors, because <see cref="Select(Type)"/> rejects such a property. Throwing rather than returning
	/// <see langword="null"/> keeps the impossible case from degrading into the silent skip this type exists
	/// to remove.
	/// </summary>
	private static EncryptionException UnreachableType(PropertyInfo property, object value) =>
		new($"[EncryptedField] property {property.DeclaringType?.FullName}.{property.Name} holds a value of " +
			$"type {value.GetType().FullName}, which is not {SupportedTypeNames}.");
}
