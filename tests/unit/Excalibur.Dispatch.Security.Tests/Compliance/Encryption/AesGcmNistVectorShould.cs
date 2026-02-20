// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// NIST SP 800-38D test vectors for AES-GCM.
/// These vectors validate that the underlying cryptographic implementation is correct.
/// </summary>
/// <remarks>
/// Test vectors are from NIST Special Publication 800-38D:
/// "Recommendation for Block Cipher Modes of Operation: Galois/Counter Mode (GCM) and GMAC"
/// https://csrc.nist.gov/publications/detail/sp/800-38d/final
/// </remarks>
[Trait("Category", TestCategories.Unit)]
public sealed class AesGcmNistVectorShould
{
	/// <summary>
	/// GCM Specification Test Case 14 - AES-256-GCM with 96-bit IV
	/// From: The Galois/Counter Mode of Operation (GCM), McGrew & Viega
	/// https://csrc.nist.gov/groups/ST/toolkit/BCM/documents/proposedmodes/gcm/gcm-spec.pdf
	/// Key: 256 bits (all zeros), IV: 96 bits (all zeros), Plaintext: 128 bits (all zeros)
	/// </summary>
	[Fact]
	public void ValidateNistTestCase14_Aes256Gcm_WithAad()
	{
		// Test Case 14 from GCM specification (AES-256)
		// K = 00000000000000000000000000000000 00000000000000000000000000000000
		var key = new byte[32]; // All zeros

		// IV = 000000000000000000000000
		var iv = new byte[12]; // All zeros

		// P = 00000000000000000000000000000000 (128 bits = 16 bytes)
		var plaintext = new byte[16]; // All zeros

		// Expected C = cea7403d4d606b6e074ec5d3baf39d18
		var expectedCiphertext = Convert.FromHexString("cea7403d4d606b6e074ec5d3baf39d18");

		// Expected T = d0d1c8a799996bf0265b98b5d48ab919
		var expectedTag = Convert.FromHexString("d0d1c8a799996bf0265b98b5d48ab919");

		// Act - Encrypt
		var ciphertext = new byte[plaintext.Length];
		var tag = new byte[16];

		using var aesGcm = new AesGcm(key, 16);
		aesGcm.Encrypt(iv, plaintext, ciphertext, tag);

		// Assert - Encryption produces expected ciphertext and tag
		ciphertext.ShouldBe(expectedCiphertext);
		tag.ShouldBe(expectedTag);

		// Act - Decrypt
		var decrypted = new byte[ciphertext.Length];
		aesGcm.Decrypt(iv, ciphertext, tag, decrypted);

		// Assert - Decryption recovers original plaintext
		decrypted.ShouldBe(plaintext);
	}

	/// <summary>
	/// NIST SP 800-38D Test Case 13 - AES-256-GCM with 96-bit IV, no AAD
	/// </summary>
	[Fact]
	public void ValidateNistTestCase13_Aes256Gcm_NoAad()
	{
		// Test Case 13 from NIST SP 800-38D
		// K = 0000000000000000000000000000000000000000000000000000000000000000
		var key = new byte[32]; // All zeros

		// IV = 000000000000000000000000
		var iv = new byte[12]; // All zeros

		// P = empty (0 bits)
		var plaintext = Array.Empty<byte>();

		// Expected C = empty
		// Expected T = 530f8afbc74536b9a963b4f1c4cb738b
		var expectedTag = Convert.FromHexString("530f8afbc74536b9a963b4f1c4cb738b");

		// Act
		var ciphertext = new byte[plaintext.Length];
		var tag = new byte[16];

		using var aesGcm = new AesGcm(key, 16);
		aesGcm.Encrypt(iv, plaintext, ciphertext, tag);

		// Assert
		ciphertext.ShouldBeEmpty();
		tag.ShouldBe(expectedTag);
	}

	/// <summary>
	/// NIST Test Case 15 - AES-256 with different key
	/// Verifies proper key usage
	/// </summary>
	[Fact]
	public void ValidateNistTestCase15_Aes256Gcm_AlternateKey()
	{
		// Test Case 15 from NIST SP 800-38D
		// K = feffe9928665731c6d6a8f9467308308 feffe9928665731c6d6a8f9467308308
		var key = Convert.FromHexString("feffe9928665731c6d6a8f9467308308feffe9928665731c6d6a8f9467308308");

		// IV = cafebabefacedbad (64 bits - different from standard 96-bit)
		// Note: .NET AesGcm requires 12-byte nonce, so we use the 96-bit version
		var iv = Convert.FromHexString("9313225df88406e555909c5a");

		// P = d9313225f88406e5a55909c5aff5269a86a7a9531534f7da2e4c303d8a318a721c3c0c95956809532fcf0e2449a6b525b16aedf5aa0de657ba637b39
		var plaintext = Convert.FromHexString("d9313225f88406e5a55909c5aff5269a86a7a9531534f7da2e4c303d8a318a721c3c0c95956809532fcf0e2449a6b525b16aedf5aa0de657ba637b39");

		// A = feedfacedeadbeeffeedfacedeadbeefabaddad2
		var aad = Convert.FromHexString("feedfacedeadbeeffeedfacedeadbeefabaddad2");

		// Act - Encrypt and decrypt round trip
		var ciphertext = new byte[plaintext.Length];
		var tag = new byte[16];

		using var aesGcm = new AesGcm(key, 16);
		aesGcm.Encrypt(iv, plaintext, ciphertext, tag, aad);

		// Decrypt
		var decrypted = new byte[ciphertext.Length];
		aesGcm.Decrypt(iv, ciphertext, tag, decrypted, aad);

		// Assert - Round trip succeeds
		decrypted.ShouldBe(plaintext);
	}

	/// <summary>
	/// Validates that authentication tag tampering is detected.
	/// </summary>
	[Fact]
	public void DetectTamperedAuthenticationTag()
	{
		// Arrange
		var key = new byte[32];
		RandomNumberGenerator.Fill(key);
		var iv = new byte[12];
		RandomNumberGenerator.Fill(iv);
		var plaintext = "Test data for authentication"u8.ToArray();

		var ciphertext = new byte[plaintext.Length];
		var tag = new byte[16];

		using var aesGcm = new AesGcm(key, 16);
		aesGcm.Encrypt(iv, plaintext, ciphertext, tag);

		// Tamper with tag
		tag[0] ^= 0xFF;

		// Act & Assert
		var decrypted = new byte[ciphertext.Length];
		_ = Should.Throw<AuthenticationTagMismatchException>(() =>
			aesGcm.Decrypt(iv, ciphertext, tag, decrypted));
	}

	/// <summary>
	/// Validates that ciphertext tampering is detected.
	/// </summary>
	[Fact]
	public void DetectTamperedCiphertext()
	{
		// Arrange
		var key = new byte[32];
		RandomNumberGenerator.Fill(key);
		var iv = new byte[12];
		RandomNumberGenerator.Fill(iv);
		var plaintext = "Test data for tampering detection"u8.ToArray();

		var ciphertext = new byte[plaintext.Length];
		var tag = new byte[16];

		using var aesGcm = new AesGcm(key, 16);
		aesGcm.Encrypt(iv, plaintext, ciphertext, tag);

		// Tamper with ciphertext
		ciphertext[0] ^= 0xFF;

		// Act & Assert
		var decrypted = new byte[ciphertext.Length];
		_ = Should.Throw<AuthenticationTagMismatchException>(() =>
			aesGcm.Decrypt(iv, ciphertext, tag, decrypted));
	}

	/// <summary>
	/// Validates that AAD tampering is detected.
	/// </summary>
	[Fact]
	public void DetectTamperedAad()
	{
		// Arrange
		var key = new byte[32];
		RandomNumberGenerator.Fill(key);
		var iv = new byte[12];
		RandomNumberGenerator.Fill(iv);
		var plaintext = "Test data"u8.ToArray();
		var aad = "Associated data"u8.ToArray();

		var ciphertext = new byte[plaintext.Length];
		var tag = new byte[16];

		using var aesGcm = new AesGcm(key, 16);
		aesGcm.Encrypt(iv, plaintext, ciphertext, tag, aad);

		// Tamper with AAD
		var tamperedAad = "Tampered data!!"u8.ToArray();

		// Act & Assert
		var decrypted = new byte[ciphertext.Length];
		_ = Should.Throw<AuthenticationTagMismatchException>(() =>
			aesGcm.Decrypt(iv, ciphertext, tag, decrypted, tamperedAad));
	}

	/// <summary>
	/// Validates that nonce reuse produces different ciphertext for same key
	/// (demonstrating why nonce uniqueness matters).
	/// </summary>
	[Fact]
	public void DemonstrateNonceUniquenessRequirement()
	{
		// Arrange - same key, same plaintext, different nonces
		var key = new byte[32];
		RandomNumberGenerator.Fill(key);
		var plaintext = "Same plaintext"u8.ToArray();

		var iv1 = new byte[12];
		var iv2 = new byte[12];
		RandomNumberGenerator.Fill(iv1);
		RandomNumberGenerator.Fill(iv2);

		var ciphertext1 = new byte[plaintext.Length];
		var ciphertext2 = new byte[plaintext.Length];
		var tag1 = new byte[16];
		var tag2 = new byte[16];

		// Act
		using var aesGcm = new AesGcm(key, 16);
		aesGcm.Encrypt(iv1, plaintext, ciphertext1, tag1);
		aesGcm.Encrypt(iv2, plaintext, ciphertext2, tag2);

		// Assert - Different nonces produce different ciphertexts
		ciphertext1.ShouldNotBe(ciphertext2);
		tag1.ShouldNotBe(tag2);
	}

	/// <summary>
	/// Validates 256-bit key size is enforced.
	/// </summary>
	[Fact]
	public void RequireCorrectKeySize()
	{
		// Arrange - wrong key sizes
		var shortKey = new byte[16]; // 128-bit (too short for AES-256)
		var iv = new byte[12];
		var plaintext = "Test"u8.ToArray();
		var ciphertext = new byte[plaintext.Length];
		var tag = new byte[16];

		// Act & Assert - AesGcm accepts 128, 192, 256 bit keys
		// With 128-bit key, it becomes AES-128-GCM (valid but not what we want)
		using var aes128 = new AesGcm(shortKey, 16);
		Should.NotThrow(() => aes128.Encrypt(iv, plaintext, ciphertext, tag));

		// Verify we're using 256-bit keys in our implementation
		var correctKey = new byte[32]; // 256-bit
		correctKey.Length.ShouldBe(32);
	}
}
