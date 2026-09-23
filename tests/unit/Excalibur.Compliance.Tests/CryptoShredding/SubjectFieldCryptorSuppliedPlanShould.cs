// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Compliance.CryptoShredding;
using Excalibur.Compliance.Encryption;

namespace Excalibur.Compliance.Tests.CryptoShredding;

/// <summary>
/// Exercises the crypto-shredding encrypt and decrypt paths against a SUPPLIED field plan, over a type that
/// carries no <see cref="PersonalDataAttribute"/> at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a supplied plan exists.</b> The public path derives its plan by reflecting over a record's
/// annotations, so testing it means compiling a <c>[PersonalData]</c> type into a test assembly. The erasure
/// coverage scan enumerates every loaded assembly and correctly reports that type as annotated personal data
/// that no discovered location covers — so any such fixture, anywhere the scan reaches, is a genuine finding
/// against the gate. The gate is right and the fixture was unavoidable; the missing piece was a way to drive
/// the logic without the annotation.
/// </para>
/// <para>
/// <b>What this does NOT replace.</b> The reflection path — which properties the annotations select — is still
/// covered by the annotated fixtures beside this file. These arms cover what happens to a record once a plan
/// exists, which is the part that needed a fixture only by accident.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SubjectFieldCryptorSuppliedPlanShould
{
	private const string Plaintext = "ada@example.com";

	private static readonly PropertyInfo SubjectId = typeof(UnannotatedCustomer).GetProperty(nameof(UnannotatedCustomer.SubjectId))!;
	private static readonly PropertyInfo Email = typeof(UnannotatedCustomer).GetProperty(nameof(UnannotatedCustomer.Email))!;
	private static readonly PropertyInfo Age = typeof(UnannotatedCustomer).GetProperty(nameof(UnannotatedCustomer.Age))!;

	/// <summary>
	/// The premise every other arm rests on. If this fixture ever gains the attribute, this file trips the
	/// coverage gate exactly as the annotated fixtures do, and the seam has bought nothing.
	/// </summary>
	[Fact]
	public void Use_a_fixture_that_declares_no_personal_data()
	{
		typeof(UnannotatedCustomer)
			.GetProperties()
			.Where(p => p.GetCustomAttribute<PersonalDataAttribute>() is not null)
			.ShouldBeEmpty(
				"this fixture exists precisely so the crypto-shredding path can be exercised WITHOUT an "
				+ "annotated type in a scanned assembly; annotating it re-creates the coverage-gate violation.");
	}

	/// <summary>SAFETY. A supplied plan is honoured: the field it names is encrypted and marked.</summary>
	[Fact]
	public async Task Encrypt_the_field_a_supplied_plan_names()
	{
		var record = new UnannotatedCustomer { SubjectId = "subject-1", Email = Plaintext };

		await Cryptor()
			.EncryptFieldsAsync(record, SubjectFieldCryptor.TypeFieldPlan.Describe(SubjectId, Email), CancellationToken.None)
			.ConfigureAwait(false);

		record.Email!.StartsWith(EncryptedFieldBinding.StringEnvelopePrefix, StringComparison.Ordinal).ShouldBeTrue(
			"the plan named Email as personal data, so it must leave the encrypt path as a marked envelope. A "
			+ "plan that is silently ignored would leave it as plaintext.");
	}

	/// <summary>LIVENESS. The value survives the round trip, so "encrypt into something unreadable" cannot pass.</summary>
	[Fact]
	public async Task Restore_the_original_value_on_decrypt()
	{
		var plan = SubjectFieldCryptor.TypeFieldPlan.Describe(SubjectId, Email);
		var cryptor = Cryptor();
		var record = new UnannotatedCustomer { SubjectId = "subject-1", Email = Plaintext };

		await cryptor.EncryptFieldsAsync(record, plan, CancellationToken.None).ConfigureAwait(false);
		await cryptor.DecryptFieldsAsync(record, plan, CancellationToken.None).ConfigureAwait(false);

		record.Email.ShouldBe(Plaintext);
	}

	/// <summary>
	/// SAFETY. The fail-closed guard is a property of the plan, not of reflection: a plan with a subject and no
	/// personal-data fields is refused rather than persisting plaintext, however the plan was produced.
	/// </summary>
	[Fact]
	public async Task Refuse_a_plan_that_names_a_subject_but_no_personal_data()
	{
		var record = new UnannotatedCustomer { SubjectId = "subject-1", Email = Plaintext };

		_ = await Should.ThrowAsync<EncryptionException>(async () =>
			await Cryptor()
				.EncryptFieldsAsync(record, SubjectFieldCryptor.TypeFieldPlan.Describe(SubjectId), CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);

		record.Email.ShouldBe(Plaintext, "a refused encrypt must not have half-written the record");
	}

	/// <summary>
	/// A supplied plan passes through the SAME encryptability rule reflection applies, so it cannot name a
	/// field the encrypt path would mishandle.
	/// </summary>
	[Fact]
	public void Reject_a_plan_naming_a_field_that_cannot_carry_an_envelope() =>
		Should.Throw<ArgumentException>(() => SubjectFieldCryptor.TypeFieldPlan.Describe(SubjectId, Age));

	private static SubjectFieldCryptor Cryptor()
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

		return new SubjectFieldCryptor(encryptor);
	}

	// Deliberately carries NO [PersonalData] and NO [DataSubjectId]: the plan says which is which.
	private sealed class UnannotatedCustomer
	{
		public string SubjectId { get; set; } = string.Empty;

		public string? Email { get; set; }

		public int Age { get; set; }
	}
}
