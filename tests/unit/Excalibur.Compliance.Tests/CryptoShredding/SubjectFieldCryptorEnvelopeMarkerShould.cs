// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.CryptoShredding;
using Excalibur.Compliance.Encryption;

namespace Excalibur.Compliance.Tests.CryptoShredding;

/// <summary>
/// Binds both halves of the envelope marker on crypto-shredded fields: our writes are MARKED, and a value
/// written before the marker existed is still READABLE.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why both arms, and why neither is sufficient alone.</strong> The marker exists so a reader can
/// tell a stored ciphertext from a value that was never encrypted — bare Base64 cannot express that
/// difference, because plaintext can be valid Base64 by coincidence. Marking the write is the safety half.
/// But an implementation that marks its writes and can no longer read anything written earlier has traded
/// one silent failure for a worse one: every field stored before the upgrade becomes unreadable, and the
/// data is gone rather than merely mislabelled. So the backward-compatible read is not a nicety here, it is
/// the liveness half, and a suite asserting only the marker would pass while deleting a consumer's history.
/// </para>
/// <para>
/// <strong>How the legacy value is constructed.</strong> The second arm encrypts through the real writer and
/// then STRIPS the prefix, rather than hand-rolling a serialized envelope. That matters: a hand-built
/// fixture encodes this test's belief about the envelope format, so it would keep passing if the format
/// changed underneath it and would be testing itself. Stripping the marker from a genuine write produces
/// exactly the shape the previous version stored, and stays correct as the format evolves.
/// </para>
/// <para>
/// <strong>The fake is a round-trip, not an assertion target.</strong> It carries the plaintext through as
/// the ciphertext, so the bytes that come back prove the value travelled through the binding's write and
/// read paths. Nothing here asserts on the fake; real cryptography is covered elsewhere and would only
/// obscure which layer failed.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SubjectFieldCryptorEnvelopeMarkerShould
{
	private const string Plaintext = "ada@example.com";

	/// <summary>
	/// SAFETY. A field this framework encrypted carries the marker, so a reader is never left guessing
	/// whether a stored string is ciphertext or a value nobody protected.
	/// </summary>
	[Fact]
	public async Task Mark_a_field_it_encrypted_so_a_reader_can_tell_ciphertext_from_untouched_plaintext()
	{
		var cryptor = new SubjectFieldCryptor(RoundTrippingEncryptor());
		var record = new Customer { SubjectId = "subject-1", Email = Plaintext };

		await cryptor.EncryptFieldsAsync(record, CancellationToken.None).ConfigureAwait(false);

		record.Email!.StartsWith(EncryptedFieldBinding.StringEnvelopePrefix, StringComparison.Ordinal)
			.ShouldBeTrue(
				"a field this framework encrypted must say so in the stored value. Without the marker a "
				+ "reader cannot distinguish our ciphertext from a value that was never encrypted, because "
				+ "Base64 is a shape plaintext can have by accident — and the reader that guesses wrong "
				+ "hands back either an unreadable blob or, far worse, personal data it believed was "
				+ "protected.");

		record.Email.Contains(Plaintext, StringComparison.Ordinal)
			.ShouldBeFalse("the stored value must not still contain the plaintext it replaced.");
	}

	/// <summary>
	/// LIVENESS. A value stored before the marker existed is still decryptable, so upgrading does not
	/// orphan every field written by the previous version.
	/// </summary>
	[Fact]
	public async Task Still_decrypt_a_value_written_before_the_marker_existed()
	{
		var cryptor = new SubjectFieldCryptor(RoundTrippingEncryptor());
		var record = new Customer { SubjectId = "subject-1", Email = Plaintext };

		await cryptor.EncryptFieldsAsync(record, CancellationToken.None).ConfigureAwait(false);

		// Reproduce the legacy on-disk shape from a genuine write: same envelope, no marker. This is what
		// the previous version persisted, and it is what a consumer's database is full of on the day they
		// upgrade.
		record.Email!.StartsWith(EncryptedFieldBinding.StringEnvelopePrefix, StringComparison.Ordinal)
			.ShouldBeTrue(
				"premise of this arm: the write must be marked before stripping the marker can simulate "
				+ "the legacy form. If this fails the arm below is stripping nothing and proves nothing.");

		record.Email = record.Email[EncryptedFieldBinding.StringEnvelopePrefix.Length..];

		await cryptor.DecryptFieldsAsync(record, CancellationToken.None).ConfigureAwait(false);

		record.Email.ShouldBe(
			Plaintext,
			"a field written before the marker existed must still be readable. If the unmarked branch stops "
			+ "resolving, every value a consumer stored with the previous version becomes undecryptable on "
			+ "upgrade — which is data loss, not a compatibility nit, and it is invisible until someone "
			+ "tries to read an old record.");
	}

	/// <summary>
	/// Carries the plaintext through as the ciphertext, so a decrypt returns what the encrypt was given and
	/// the arms observe the BINDING rather than a cipher.
	/// </summary>
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
