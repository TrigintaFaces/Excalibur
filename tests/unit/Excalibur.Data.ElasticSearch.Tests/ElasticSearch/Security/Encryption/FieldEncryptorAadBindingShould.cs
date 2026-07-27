// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security;

using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.ElasticSearch.Tests.ElasticSearch.Security.Encryption;

/// <summary>
/// Regression lock (bead 9so1s5 part-2, author≠impl) for AES-GCM Associated Data (AAD) binding in
/// <see cref="FieldEncryptor"/>: the field name is part of the AAD, so a ciphertext encrypted for one field
/// MUST NOT authenticate when replayed under a different field name (cross-field ciphertext swap).
/// </summary>
/// <remarks>
/// <b>Defect (pre-fix):</b> AES-GCM ran with no associated data, so the auth tag bound only the bytes — a
/// ciphertext could be lifted from field <c>ssn</c> and pasted into field <c>email</c> and would still
/// decrypt. <b>Fix:</b> AAD = <c>fieldName ␟ keyVersion ␟ algorithm ␟ classification</c> (0x1F-joined,
/// reconstructed identically at decrypt from the persisted envelope) → the field name is authenticated.
/// <para>
/// <b>Non-vacuity:</b> the control (decrypt under the SAME field name) round-trips, and the tamper (decrypt
/// the SAME envelope under a DIFFERENT field name — key version, algorithm, classification and ciphertext all
/// unchanged, so the ONLY differing AAD component is the field name) throws. On the pre-fix no-AAD path the
/// field-name swap decrypts cleanly → RED. Field-name swap is the unambiguous AAD test: tampering keyVersion
/// or classification would instead change key/format resolution (a different failure path), so it isn't a
/// clean AAD proof.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Encryption")]
public sealed class FieldEncryptorAadBindingShould
{
	private const string PlainText = "123-45-6789";

	[Fact]
	public async Task RejectACiphertextReplayedUnderADifferentFieldName()
	{
		using var sut = CreateEncryptor();

		// Encrypt for field "ssn".
		var encrypted = await sut.EncryptFieldAsync(
			"ssn", PlainText, ElasticSearchDataClassification.Confidential, CancellationToken.None);

		// Control — the SAME field name authenticates and round-trips (proves the tamper isn't a broken decrypt).
		var roundTrip = await sut.DecryptFieldAsync("ssn", encrypted, CancellationToken.None);
		_ = roundTrip.ShouldNotBeNull();
		roundTrip.ToString().ShouldBe(PlainText);

		// Tamper — replay the EXACT same envelope under a different field name. Only the AAD's field-name
		// component differs → the GCM auth tag fails. RED on the pre-fix no-AAD path (it would decrypt).
		_ = await Should.ThrowAsync<SecurityException>(
			() => sut.DecryptFieldAsync("email", encrypted, CancellationToken.None));
	}

	private static FieldEncryptor CreateEncryptor() =>
		new(
			new LocalKeyProvider(),
			Options.Create(new EncryptionOptions
			{
				KeyManagement = new KeyManagementOptions { KeyRotationInterval = TimeSpan.Zero },
			}),
			NullLogger<FieldEncryptor>.Instance);
}
