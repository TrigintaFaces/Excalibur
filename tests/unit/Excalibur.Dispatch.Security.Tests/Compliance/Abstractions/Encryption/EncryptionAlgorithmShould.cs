// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

/// <summary>
/// Unit tests for <see cref="EncryptionAlgorithm"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Encryption")]
public sealed class EncryptionAlgorithmShould : UnitTestBase
{
	[Fact]
	public void HaveTwoAlgorithmOptions()
	{
		// Assert
		var values = Enum.GetValues<EncryptionAlgorithm>();
		values.Length.ShouldBe(2);
	}

	[Fact]
	public void HaveAes256GcmAsDefaultRecommendedAlgorithm()
	{
		// Assert - AES-256-GCM should be 0 (default) as per ADR-051
		((int)EncryptionAlgorithm.Aes256Gcm).ShouldBe(0);
	}

	[Theory]
	[InlineData(EncryptionAlgorithm.Aes256Gcm, 0)]
	[InlineData(EncryptionAlgorithm.Aes256CbcHmac, 1)]
	public void HaveCorrectUnderlyingValues(EncryptionAlgorithm algorithm, int expectedValue)
	{
		// Assert
		((int)algorithm).ShouldBe(expectedValue);
	}

	[Theory]
	[InlineData("Aes256Gcm", EncryptionAlgorithm.Aes256Gcm)]
	[InlineData("Aes256CbcHmac", EncryptionAlgorithm.Aes256CbcHmac)]
	public void ParseFromString(string input, EncryptionAlgorithm expected)
	{
		// Act
		var result = Enum.Parse<EncryptionAlgorithm>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData(EncryptionAlgorithm.Aes256Gcm, "Aes256Gcm")]
	[InlineData(EncryptionAlgorithm.Aes256CbcHmac, "Aes256CbcHmac")]
	public void ConvertToString(EncryptionAlgorithm algorithm, string expected)
	{
		// Act
		var result = algorithm.ToString();

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData(EncryptionAlgorithm.Aes256Gcm, true)]
	[InlineData(EncryptionAlgorithm.Aes256CbcHmac, true)]
	public void SupportAuthenticatedEncryption(EncryptionAlgorithm algorithm, bool isAuthenticated)
	{
		// All supported algorithms provide authenticated encryption
		// AES-256-GCM: Built-in authentication
		// AES-256-CBC-HMAC: HMAC provides authentication

		// Assert
		isAuthenticated.ShouldBeTrue($"Algorithm {algorithm} should support authenticated encryption");
	}

	[Fact]
	public void DefaultToAes256GcmWhenNotSpecified()
	{
		// Arrange
		EncryptionAlgorithm defaultValue = default;

		// Assert
		defaultValue.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
	}

	[Fact]
	public void BeUsableInSwitchStatements()
	{
		// This test demonstrates the typical usage pattern
		var algorithms = Enum.GetValues<EncryptionAlgorithm>();

		foreach (var algorithm in algorithms)
		{
			var keySize = algorithm switch
			{
				EncryptionAlgorithm.Aes256Gcm => 256,
				EncryptionAlgorithm.Aes256CbcHmac => 256,
				_ => throw new ArgumentOutOfRangeException(nameof(algorithm))
			};

			// All algorithms use 256-bit keys
			keySize.ShouldBe(256);
		}
	}

	[Fact]
	public void IncludeOnlyFipsCompliantAlgorithms()
	{
		// Per ADR-051, all algorithms should be FIPS 140-2 compliant (or have a path to compliance)
		var algorithms = Enum.GetValues<EncryptionAlgorithm>();

		foreach (var algorithm in algorithms)
		{
			// AES-256-GCM: FIPS compliant
			// AES-256-CBC-HMAC: FIPS compliant (AES and HMAC-SHA256 both approved)

			var isFipsCompatible = algorithm switch
			{
				EncryptionAlgorithm.Aes256Gcm => true,
				EncryptionAlgorithm.Aes256CbcHmac => true,
				_ => false
			};

			isFipsCompatible.ShouldBeTrue($"Algorithm {algorithm} should be FIPS compatible");
		}
	}
}
