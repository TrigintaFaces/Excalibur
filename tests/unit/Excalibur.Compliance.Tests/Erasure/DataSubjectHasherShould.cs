// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;
using System.Text;

using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Regression locks for <see cref="HmacDataSubjectHasher"/> (wrht38): the data-subject pseudonymization
/// must be a keyed HMAC-SHA-256, not a bare unsalted SHA-256, so a low-entropy identifier's token is not
/// reversible by rainbow-table / dictionary attack; and a missing/weak pepper must fail closed.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class DataSubjectHasherShould
{
	private const string Pepper = "test-pepper-0123456789abcdef0123456789ab";

	private static IDataSubjectHasher CreateHasher(string? pepper = Pepper) =>
		new HmacDataSubjectHasher(Options.Create(new DataSubjectHashingOptions { Pepper = pepper }));

	[Fact]
	public void Return_uppercase_hex_encoded_hmacsha256_hash()
	{
		var hash = CreateHasher().HashDataSubjectId("test-user-123");

		hash.ShouldNotBeNullOrWhiteSpace();
		hash.Length.ShouldBe(64); // HMAC-SHA-256 produces 32 bytes = 64 hex chars
		hash.ShouldAllBe(c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'));
	}

	[Fact]
	public void Return_consistent_hash_for_same_input_and_pepper()
	{
		var hasher = CreateHasher();

		hasher.HashDataSubjectId("user@example.com")
			.ShouldBe(hasher.HashDataSubjectId("user@example.com"));
	}

	[Fact]
	public void Return_different_hashes_for_different_inputs()
	{
		var hasher = CreateHasher();

		hasher.HashDataSubjectId("user-1").ShouldNotBe(hasher.HashDataSubjectId("user-2"));
	}

	[Fact]
	public void Be_keyed_not_a_bare_sha256_so_the_token_is_not_reversible()
	{
		const string id = "user@example.com";

		// Non-vacuity: a bare unsalted SHA-256 (the pre-fix construction) is what a rainbow table inverts.
		var bareSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)));
		var keyed = CreateHasher().HashDataSubjectId(id);

		keyed.ShouldNotBe(bareSha256);

		// Changing only the pepper changes the token — proves the hash is actually keyed by the secret.
		var otherPepper = CreateHasher("different-pepper-abcdefghijklmnop0123456789")
			.HashDataSubjectId(id);
		keyed.ShouldNotBe(otherPepper);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("too-short")]
	public void Fail_closed_when_pepper_is_missing_or_too_short(string? pepper)
	{
		// Fail-closed: never silently fall back to an unkeyed hash.
		_ = Should.Throw<InvalidOperationException>(() => CreateHasher(pepper));
	}
}
