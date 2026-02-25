// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="EncryptionAlgorithm"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Configuration")]
[Trait("Priority", "0")]
public sealed class EncryptionAlgorithmShould
{
	#region Enum Value Tests

	[Fact]
	public void None_HasExpectedValue()
	{
		// Assert
		((int)EncryptionAlgorithm.None).ShouldBe(0);
	}

	[Fact]
	public void Aes128Gcm_HasExpectedValue()
	{
		// Assert
		((int)EncryptionAlgorithm.Aes128Gcm).ShouldBe(1);
	}

	[Fact]
	public void Aes256Gcm_HasExpectedValue()
	{
		// Assert
		((int)EncryptionAlgorithm.Aes256Gcm).ShouldBe(2);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void ContainsAllExpectedValues()
	{
		// Arrange
		var values = Enum.GetValues<EncryptionAlgorithm>();

		// Assert
		values.ShouldContain(EncryptionAlgorithm.None);
		values.ShouldContain(EncryptionAlgorithm.Aes128Gcm);
		values.ShouldContain(EncryptionAlgorithm.Aes256Gcm);
	}

	[Fact]
	public void HasExactlyThreeValues()
	{
		// Arrange
		var values = Enum.GetValues<EncryptionAlgorithm>();

		// Assert
		values.Length.ShouldBe(3);
	}

	#endregion

	#region String Conversion Tests

	[Theory]
	[InlineData(EncryptionAlgorithm.None, "None")]
	[InlineData(EncryptionAlgorithm.Aes128Gcm, "Aes128Gcm")]
	[InlineData(EncryptionAlgorithm.Aes256Gcm, "Aes256Gcm")]
	public void ToString_ReturnsExpectedValue(EncryptionAlgorithm algorithm, string expected)
	{
		// Act & Assert
		algorithm.ToString().ShouldBe(expected);
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("None", EncryptionAlgorithm.None)]
	[InlineData("Aes128Gcm", EncryptionAlgorithm.Aes128Gcm)]
	[InlineData("Aes256Gcm", EncryptionAlgorithm.Aes256Gcm)]
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
	public void DefaultValue_IsNone()
	{
		// Arrange
		EncryptionAlgorithm algorithm = default;

		// Assert
		algorithm.ShouldBe(EncryptionAlgorithm.None);
	}

	#endregion
}
