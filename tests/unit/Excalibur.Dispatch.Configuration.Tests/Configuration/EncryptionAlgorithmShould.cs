// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="EncryptionAlgorithm"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Configuration)]
[Trait("Priority", "0")]
public sealed class EncryptionAlgorithmShould
{
	#region Enum Value Tests

	[Fact]
	public void Aes256Gcm_HasExpectedValue()
	{
		// Assert
		((int)EncryptionAlgorithm.Aes256Gcm).ShouldBe(0);
	}

	[Fact]
	public void Aes256CbcHmac_HasExpectedValue()
	{
		// Assert
		((int)EncryptionAlgorithm.Aes256CbcHmac).ShouldBe(1);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void ContainsAllExpectedValues()
	{
		// Arrange
		var values = Enum.GetValues<EncryptionAlgorithm>();

		// Assert
		values.ShouldContain(EncryptionAlgorithm.Aes256Gcm);
		values.ShouldContain(EncryptionAlgorithm.Aes256CbcHmac);
	}

	[Fact]
	public void HasExactlyTwoValues()
	{
		// Arrange
		var values = Enum.GetValues<EncryptionAlgorithm>();

		// Assert
		values.Length.ShouldBe(2);
	}

	#endregion

	#region String Conversion Tests

	[Theory]
	[InlineData(EncryptionAlgorithm.Aes256Gcm, "Aes256Gcm")]
	[InlineData(EncryptionAlgorithm.Aes256CbcHmac, "Aes256CbcHmac")]
	public void ToString_ReturnsExpectedValue(EncryptionAlgorithm algorithm, string expected)
	{
		// Act & Assert
		algorithm.ToString().ShouldBe(expected);
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("Aes256Gcm", EncryptionAlgorithm.Aes256Gcm)]
	[InlineData("Aes256CbcHmac", EncryptionAlgorithm.Aes256CbcHmac)]
	public void Parse_WithValidString_ReturnsExpectedValue(string value, EncryptionAlgorithm expected)
	{
		// Act
		var result = Enum.Parse<EncryptionAlgorithm>(value);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsAes256Gcm()
	{
		// Arrange
		EncryptionAlgorithm algorithm = default;

		// Assert
		algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
	}

	#endregion
}
