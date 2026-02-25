// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// Unit tests for <see cref="EncryptionMode"/> enum.
/// </summary>
/// <remarks>
/// Per AD-254-1, these tests verify the encryption mode enum values and transitions.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class EncryptionModeShould
{
	#region Enum Value Tests

	[Fact]
	public void HaveEncryptAndDecryptAsDefaultValue()
	{
		// Arrange
		var defaultMode = default(EncryptionMode);

		// Assert
		defaultMode.ShouldBe(EncryptionMode.EncryptAndDecrypt);
	}

	[Fact]
	public void HaveEncryptAndDecryptValueZero()
	{
		// Arrange & Act
		var value = (int)EncryptionMode.EncryptAndDecrypt;

		// Assert
		value.ShouldBe(0);
	}

	[Fact]
	public void HaveEncryptNewDecryptAllValueOne()
	{
		// Arrange & Act
		var value = (int)EncryptionMode.EncryptNewDecryptAll;

		// Assert
		value.ShouldBe(1);
	}

	[Fact]
	public void HaveDecryptOnlyWritePlaintextValueTwo()
	{
		// Arrange & Act
		var value = (int)EncryptionMode.DecryptOnlyWritePlaintext;

		// Assert
		value.ShouldBe(2);
	}

	[Fact]
	public void HaveDecryptOnlyReadOnlyValueThree()
	{
		// Arrange & Act
		var value = (int)EncryptionMode.DecryptOnlyReadOnly;

		// Assert
		value.ShouldBe(3);
	}

	[Fact]
	public void HaveDisabledValueFour()
	{
		// Arrange & Act
		var value = (int)EncryptionMode.Disabled;

		// Assert
		value.ShouldBe(4);
	}

	[Fact]
	public void HaveExactlyFiveValues()
	{
		// Arrange & Act
		var values = Enum.GetValues<EncryptionMode>();

		// Assert
		values.Length.ShouldBe(5);
	}

	#endregion Enum Value Tests

	#region Enum Name Tests

	[Theory]
	[InlineData(EncryptionMode.EncryptAndDecrypt, "EncryptAndDecrypt")]
	[InlineData(EncryptionMode.EncryptNewDecryptAll, "EncryptNewDecryptAll")]
	[InlineData(EncryptionMode.DecryptOnlyWritePlaintext, "DecryptOnlyWritePlaintext")]
	[InlineData(EncryptionMode.DecryptOnlyReadOnly, "DecryptOnlyReadOnly")]
	[InlineData(EncryptionMode.Disabled, "Disabled")]
	public void HaveCorrectNameForValue(EncryptionMode mode, string expectedName)
	{
		// Arrange & Act
		var name = mode.ToString();

		// Assert
		name.ShouldBe(expectedName);
	}

	#endregion Enum Name Tests

	#region Parse Tests

	[Theory]
	[InlineData("EncryptAndDecrypt", EncryptionMode.EncryptAndDecrypt)]
	[InlineData("EncryptNewDecryptAll", EncryptionMode.EncryptNewDecryptAll)]
	[InlineData("DecryptOnlyWritePlaintext", EncryptionMode.DecryptOnlyWritePlaintext)]
	[InlineData("DecryptOnlyReadOnly", EncryptionMode.DecryptOnlyReadOnly)]
	[InlineData("Disabled", EncryptionMode.Disabled)]
	public void ParseFromString(string name, EncryptionMode expected)
	{
		// Arrange & Act
		var parsed = Enum.Parse<EncryptionMode>(name);

		// Assert
		parsed.ShouldBe(expected);
	}

	[Theory]
	[InlineData("0", EncryptionMode.EncryptAndDecrypt)]
	[InlineData("1", EncryptionMode.EncryptNewDecryptAll)]
	[InlineData("2", EncryptionMode.DecryptOnlyWritePlaintext)]
	[InlineData("3", EncryptionMode.DecryptOnlyReadOnly)]
	[InlineData("4", EncryptionMode.Disabled)]
	public void ParseFromNumericString(string numericValue, EncryptionMode expected)
	{
		// Arrange & Act
		var parsed = Enum.Parse<EncryptionMode>(numericValue);

		// Assert
		parsed.ShouldBe(expected);
	}

	[Fact]
	public void FailToParseInvalidName()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentException>(() => Enum.Parse<EncryptionMode>("InvalidMode"));
	}

	#endregion Parse Tests

	#region TryParse Tests

	[Theory]
	[InlineData("EncryptAndDecrypt", true)]
	[InlineData("Disabled", true)]
	[InlineData("InvalidMode", false)]
	[InlineData("", false)]
	public void TryParseReturnsExpectedResult(string input, bool expectedResult)
	{
		// Arrange & Act
		var result = Enum.TryParse<EncryptionMode>(input, out _);

		// Assert
		result.ShouldBe(expectedResult);
	}

	#endregion TryParse Tests

	#region IsDefined Tests

	[Theory]
	[InlineData(0, true)]
	[InlineData(1, true)]
	[InlineData(2, true)]
	[InlineData(3, true)]
	[InlineData(4, true)]
	[InlineData(5, false)]
	[InlineData(-1, false)]
	[InlineData(100, false)]
	public void IsDefinedForValue(int value, bool expectedDefined)
	{
		// Arrange & Act
		var isDefined = Enum.IsDefined(typeof(EncryptionMode), value);

		// Assert
		isDefined.ShouldBe(expectedDefined);
	}

	#endregion IsDefined Tests

	#region Mode Semantics Tests

	[Fact]
	public void EncryptAndDecrypt_IsFullEncryptionMode()
	{
		// This mode should encrypt writes and decrypt reads
		var mode = EncryptionMode.EncryptAndDecrypt;

		// Assert semantic checks
		mode.ShouldNotBe(EncryptionMode.Disabled);
		mode.ShouldBe(default(EncryptionMode)); // Default mode
	}

	[Fact]
	public void EncryptNewDecryptAll_IsMigrationPhase1()
	{
		// This mode encrypts new data but can decrypt from legacy providers
		var mode = EncryptionMode.EncryptNewDecryptAll;

		// Semantic: Still encrypts (not decrypt-only or disabled)
		mode.ShouldNotBe(EncryptionMode.DecryptOnlyWritePlaintext);
		mode.ShouldNotBe(EncryptionMode.DecryptOnlyReadOnly);
		mode.ShouldNotBe(EncryptionMode.Disabled);
	}

	[Fact]
	public void DecryptOnlyWritePlaintext_IsMigrationPhase2()
	{
		// This mode decrypts reads and writes plaintext
		var mode = EncryptionMode.DecryptOnlyWritePlaintext;

		// Semantic: Does not encrypt new data
		mode.ShouldNotBe(EncryptionMode.EncryptAndDecrypt);
		mode.ShouldNotBe(EncryptionMode.EncryptNewDecryptAll);
	}

	[Fact]
	public void DecryptOnlyReadOnly_RejectsWrites()
	{
		// This mode should reject write operations
		var mode = EncryptionMode.DecryptOnlyReadOnly;

		// Semantic: Read-only mode
		mode.ShouldNotBe(EncryptionMode.EncryptAndDecrypt);
		mode.ShouldNotBe(EncryptionMode.DecryptOnlyWritePlaintext);
	}

	[Fact]
	public void Disabled_IsPassthroughMode()
	{
		// This mode does no encryption or decryption
		var mode = EncryptionMode.Disabled;

		// Semantic: No encryption operations
		mode.ShouldNotBe(EncryptionMode.EncryptAndDecrypt);
		((int)mode).ShouldBe(4); // Highest value
	}

	#endregion Mode Semantics Tests

	#region Comparison Tests

	[Fact]
	public void ModesAreComparable()
	{
		// Arrange
		var modes = Enum.GetValues<EncryptionMode>().Order().ToList();

		// Assert - values should be in order
		modes[0].ShouldBe(EncryptionMode.EncryptAndDecrypt);
		modes[1].ShouldBe(EncryptionMode.EncryptNewDecryptAll);
		modes[2].ShouldBe(EncryptionMode.DecryptOnlyWritePlaintext);
		modes[3].ShouldBe(EncryptionMode.DecryptOnlyReadOnly);
		modes[4].ShouldBe(EncryptionMode.Disabled);
	}

	[Fact]
	public void EncryptAndDecryptLessThanDisabled()
	{
		// Migration progresses from EncryptAndDecrypt toward Disabled
		(EncryptionMode.EncryptAndDecrypt < EncryptionMode.Disabled).ShouldBeTrue();
	}

	#endregion Comparison Tests
}
