// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.CryptoShredding;
using Excalibur.Compliance.Encryption;

namespace Excalibur.Compliance.Tests.CryptoShredding;

/// <summary>
/// A stored envelope this framework wrote, damaged in transit or storage, must fail closed — the caller
/// never receives the stored ciphertext in place of the plaintext.
/// </summary>
/// <remarks>
/// <para>
/// <strong>This arm is GREEN and asserts existing correct behaviour.</strong> It is a lock, not a
/// diagnosis: the guarantee below already holds, and nothing bound it until now. That is precisely why it
/// is worth having — three separate readers examined this path in one evening and each concluded it was a
/// fail-open. It is not, and an arm that says so mechanically outlasts any comment saying so in prose.
/// </para>
/// <para>
/// <strong>What makes it fail closed, and why it is easy to misread.</strong> A stored string carries the
/// marker, then Base64 of the framed envelope, and the framed bytes begin with the <c>EXCR</c> magic. A
/// tail-truncated value therefore still <i>decodes</i> and still <i>starts with the magic</i>, so
/// <c>IsFieldEncrypted</c> — a four-byte PREFIX comparison, not a structural validation — returns true.
/// The value never reaches the <c>continue</c> that skips unrecognised data; it reaches deserialization,
/// which rejects the malformed remainder. The caller is told, rather than handed the stored bytes.
/// </para>
/// <para>
/// <strong>What this arm deliberately does NOT assert.</strong> A value that is not valid Base64 at all
/// cannot be distinguished, from the value alone, from legacy plaintext that was simply never encrypted —
/// and returning that untouched is REQUIRED, not a leak. An earlier version of this arm asserted the
/// caller must never receive the stored value in that branch too; that property cannot hold without
/// throwing on every never-encrypted field, and it was withdrawn. The line between the two cases is
/// decodability plus the magic prefix, and this arm sits firmly on the decidable side of it.
/// </para>
/// <para>
/// <strong>Truncating on a 4-byte boundary is load-bearing, not incidental.</strong> Cutting the Base64 at
/// an arbitrary offset can leave a length that is not a multiple of four, which fails to decode at all and
/// lands in the undecidable branch above — testing something else entirely. The arithmetic below keeps the
/// value decodable so the arm exercises the guarantee it names.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SubjectFieldCryptorTruncatedEnvelopeFailsClosedShould
{
	private const string Plaintext = "ada@example.com";

	[Fact]
	public async Task FailClosed_RatherThanReturningTheStoredCiphertext_WhenAnEnvelopeIsTruncated()
	{
		var cryptor = new SubjectFieldCryptor(RoundTrippingEncryptor());
		var record = new Customer { SubjectId = "subject-1", Email = Plaintext };

		await cryptor.EncryptFieldsAsync(record, CancellationToken.None).ConfigureAwait(false);

		// Build the legacy damaged form from a GENUINE write: strip the marker (what the previous version
		// stored) and truncate the payload (what a partial write, a column length limit, or a bad migration
		// leaves behind). Hand-building the bytes would encode this test's guess about the envelope format;
		// damaging a real one stays correct as the format evolves.
		var stored = record.Email!;
		stored.StartsWith(EncryptedFieldBinding.StringEnvelopePrefix, StringComparison.Ordinal)
			.ShouldBeTrue("premise: the write must be marked before stripping the marker means anything");

		var legacyTruncated = stored[EncryptedFieldBinding.StringEnvelopePrefix.Length..];
		legacyTruncated = legacyTruncated[..((legacyTruncated.Length / 2) / 4 * 4)];
		record.Email = legacyTruncated;

		// EITHER honest outcome is accepted. What is not accepted is returning normally with the ciphertext
		// still sitting in the field.
		var threw = false;
		try
		{
			await cryptor.DecryptFieldsAsync(record, CancellationToken.None).ConfigureAwait(false);
		}
		catch (Exception)
		{
			// Failing closed is a correct disposition: the caller is told the value is unavailable rather
			// than being handed something that is not the plaintext.
			threw = true;
		}

		if (threw)
		{
			return;
		}

		record.Email.ShouldNotBe(
			legacyTruncated,
			"decryption could not complete, and the field was left holding the stored ciphertext. The caller "
			+ "reads this property expecting personal data and receives the stored bytes instead — it will "
			+ "log them, render them, or forward them believing they are the plaintext. A decrypt that cannot "
			+ "do its job must say so, by throwing or by clearing the field; silently returning with the "
			+ "stored value in place makes 'decrypted' and 'skipped' the same observation to the caller.");
	}

	/// <summary>Carries the plaintext through as the ciphertext so the arm observes the BINDING, not a cipher.</summary>
	/// <returns>A field encryptor that round-trips without performing real cryptography.</returns>
	private static IFieldEncryptor RoundTrippingEncryptor()
	{
		var encryptor = A.Fake<IFieldEncryptor>();

#pragma warning disable CA2012 // FakeItEasy stores the ValueTask rather than awaiting it here
		A.CallTo(() => encryptor.EncryptAsync(A<string>._, A<ReadOnlyMemory<byte>>._, A<CancellationToken>._))
			.ReturnsLazily((string _, ReadOnlyMemory<byte> plaintext, CancellationToken _) =>
				new EncryptedData
				{
					Ciphertext = plaintext.ToArray(),
					KeyId = "test-key",
					KeyVersion = 1,
					Algorithm = EncryptionAlgorithm.Aes256Gcm,
					Iv = [],
				});

		A.CallTo(() => encryptor.DecryptAsync(A<EncryptedData>._, A<CancellationToken>._))
			.ReturnsLazily((EncryptedData envelope, CancellationToken _) => envelope.Ciphertext);
#pragma warning restore CA2012

		return encryptor;
	}

	private sealed class Customer
	{
		[DataSubjectId]
		public string SubjectId { get; set; } = string.Empty;

		[PersonalData]
		public string? Email { get; set; }
	}
}
