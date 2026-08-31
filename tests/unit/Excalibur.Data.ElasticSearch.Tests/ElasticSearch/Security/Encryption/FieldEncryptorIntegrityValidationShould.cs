// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;

using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.ElasticSearch.Tests.ElasticSearch.Security.Encryption;

/// <summary>
/// Regression lock for <see cref="FieldEncryptor.ValidateIntegrityAsync"/>: the method must authenticate the
/// AES-GCM tag against the ciphertext, not merely observe that a tag is present and Base64-shaped.
/// </summary>
/// <remarks>
/// <b>Defect (pre-fix):</b> the method decoded the authentication tag and returned <c>true</c> whenever
/// <c>Convert.FromBase64String</c> did not throw. The tag was never used to authenticate anything, so any
/// well-formed Base64 string an attacker substituted was reported as valid integrity. The method was public,
/// shipped, and advertised as the way to detect tampering without decrypting -- the audit use case.
/// <para>
/// <b>Non-vacuity (measured against the pre-fix body):</b> four arms go RED --
/// <see cref="ReportTamperedWhenTheAuthenticationTagIsSubstitutedForAnotherWellFormedTag"/>,
/// <see cref="ReportTamperedWhenTheCiphertextIsAltered"/>,
/// <see cref="ReportTamperedWhenTheFieldIsValidatedUnderADifferentName"/>, and
/// <see cref="RefuseAnEnvelopeWithAnUnrecognizedFormatVersion"/>, each returning <c>true</c> where the fixed
/// implementation returns <c>false</c>. The substituted-tag arm is the one that binds the defect most
/// precisely: its tag is a valid 16-byte Base64 tag, so it cannot be rejected for shape reasons and only real
/// tag verification distinguishes it. <see cref="ValidateAGenuineUntamperedField"/>,
/// <see cref="RefuseAnEnvelopeThatCarriesNoAuthenticationTag"/> and <see cref="RejectAMissingFieldName"/> pass
/// both before and after, as they should -- they bind behavior that was already correct.
/// </para>
/// <para>
/// <b>Safety and liveness:</b> the tamper arms assert that corruption is refused;
/// <see cref="ValidateAGenuineUntamperedField"/> asserts that an intact field still verifies. Without the
/// second, an implementation that returned <c>false</c> unconditionally would satisfy every other assertion
/// here.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Encryption")]
public sealed class FieldEncryptorIntegrityValidationShould
{
	private const string FieldName = "ssn";
	private const string PlainText = "123-45-6789";

	/// <summary>
	/// Liveness. An intact field verifies -- without this arm, a validator that always returned false would
	/// pass every tamper arm below.
	/// </summary>
	[Fact]
	public async Task ValidateAGenuineUntamperedField()
	{
		using var sut = CreateEncryptor();
		var encrypted = await EncryptAsync(sut);

		var valid = await sut.ValidateIntegrityAsync(FieldName, encrypted, CancellationToken.None);

		valid.ShouldBeTrue("an untampered field must verify, or the validator is simply refusing everything.");
	}

	/// <summary>
	/// Safety, and the exact defect: a substituted but well-formed Base64 tag was reported as valid integrity.
	/// </summary>
	[Fact]
	public async Task ReportTamperedWhenTheAuthenticationTagIsSubstitutedForAnotherWellFormedTag()
	{
		using var sut = CreateEncryptor();
		var encrypted = await EncryptAsync(sut);

		// A syntactically perfect 16-byte AES-GCM tag that was never computed over this ciphertext -- what an
		// attacker rewriting the stored envelope would produce. The pre-fix implementation Base64-decoded this
		// successfully and returned true.
		var substituted = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
		substituted.ShouldNotBe(encrypted.AuthenticationTag, "the substituted tag must actually differ.");
		Convert.FromBase64String(substituted).Length.ShouldBe(16, "the substituted tag must be shape-valid.");

		var tampered = WithAuthenticationTag(encrypted, substituted);

		var valid = await sut.ValidateIntegrityAsync(FieldName, tampered, CancellationToken.None);

		valid.ShouldBeFalse(
			"this tag does not authenticate this ciphertext; reporting it valid is the shipped defect. "
			+ "A validator that only checks Base64 shape returns true here.");
	}

	/// <summary>
	/// Safety. Altering the ciphertext while leaving the genuine tag in place must not verify.
	/// </summary>
	[Fact]
	public async Task ReportTamperedWhenTheCiphertextIsAltered()
	{
		using var sut = CreateEncryptor();
		var encrypted = await EncryptAsync(sut);

		var bytes = Convert.FromBase64String(encrypted.EncryptedValue);
		bytes[0] ^= 0xFF;

		var tampered = new EncryptedFieldResult(
			Convert.ToBase64String(bytes),
			encrypted.Algorithm,
			encrypted.KeyVersion,
			encrypted.InitializationVector,
			encrypted.AuthenticationTag,
			encrypted.Classification,
			encrypted.FormatVersion);

		var valid = await sut.ValidateIntegrityAsync(FieldName, tampered, CancellationToken.None);

		valid.ShouldBeFalse("flipped ciphertext bits must fail the authentication tag.");
	}

	/// <summary>
	/// Safety. The field name is bound into the associated data, so a ciphertext lifted into another field
	/// must not verify there.
	/// </summary>
	[Fact]
	public async Task ReportTamperedWhenTheFieldIsValidatedUnderADifferentName()
	{
		using var sut = CreateEncryptor();
		var encrypted = await EncryptAsync(sut);

		var valid = await sut.ValidateIntegrityAsync("email", encrypted, CancellationToken.None);

		valid.ShouldBeFalse("a ciphertext replayed under a different field name must not authenticate.");
	}

	/// <summary>
	/// Fail closed. An envelope carrying no tag has not been shown to be intact, so it is not reported valid.
	/// </summary>
	[Fact]
	public async Task RefuseAnEnvelopeThatCarriesNoAuthenticationTag()
	{
		using var sut = CreateEncryptor();
		var encrypted = await EncryptAsync(sut);

		var unprotectedField = WithAuthenticationTag(encrypted, authenticationTag: null);

		var valid = await sut.ValidateIntegrityAsync(FieldName, unprotectedField, CancellationToken.None);

		valid.ShouldBeFalse("integrity that was never established must not be reported as valid.");
	}

	/// <summary>
	/// Fail closed. An envelope this build cannot interpret cannot be authenticated.
	/// </summary>
	[Fact]
	public async Task RefuseAnEnvelopeWithAnUnrecognizedFormatVersion()
	{
		using var sut = CreateEncryptor();
		var encrypted = await EncryptAsync(sut);

		var future = new EncryptedFieldResult(
			encrypted.EncryptedValue,
			encrypted.Algorithm,
			encrypted.KeyVersion,
			encrypted.InitializationVector,
			encrypted.AuthenticationTag,
			encrypted.Classification,
			"999");

		var valid = await sut.ValidateIntegrityAsync(FieldName, future, CancellationToken.None);

		valid.ShouldBeFalse("an envelope format this build cannot open has not been verified.");
	}

	[Fact]
	public async Task RejectAMissingFieldName()
	{
		using var sut = CreateEncryptor();
		var encrypted = await EncryptAsync(sut);

		_ = await Should.ThrowAsync<ArgumentException>(
			() => sut.ValidateIntegrityAsync(string.Empty, encrypted, CancellationToken.None));
	}

	private static Task<EncryptedFieldResult> EncryptAsync(FieldEncryptor sut) =>
		sut.EncryptFieldAsync(
			FieldName, PlainText, ElasticSearchDataClassification.Confidential, CancellationToken.None);

	private static EncryptedFieldResult WithAuthenticationTag(
		EncryptedFieldResult source,
		string? authenticationTag) =>
		new(
			source.EncryptedValue,
			source.Algorithm,
			source.KeyVersion,
			source.InitializationVector,
			authenticationTag,
			source.Classification,
			source.FormatVersion);

	private static FieldEncryptor CreateEncryptor() =>
		new(
			new LocalKeyProvider(),
			Options.Create(new EncryptionOptions
			{
				KeyManagement = new KeyManagementOptions { KeyRotationInterval = TimeSpan.Zero },
			}),
			NullLogger<FieldEncryptor>.Instance);
}
