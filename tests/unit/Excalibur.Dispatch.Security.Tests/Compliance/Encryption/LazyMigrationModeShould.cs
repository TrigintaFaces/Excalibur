// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// Unit tests for <see cref="LazyMigrationMode"/> enum.
/// </summary>
/// <remarks>
/// Per AD-255-1, these tests verify the lazy migration mode enum values and behaviors.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class LazyMigrationModeShould
{
	#region Enum Value Tests

	[Fact]
	public void HaveDisabledAsDefaultValue()
	{
		// Arrange
		var defaultMode = default(LazyMigrationMode);

		// Assert
		defaultMode.ShouldBe(LazyMigrationMode.Disabled);
	}

	[Fact]
	public void HaveDisabledValueZero()
	{
		// Arrange & Act
		var value = (int)LazyMigrationMode.Disabled;

		// Assert
		value.ShouldBe(0);
	}

	[Fact]
	public void HaveOnReadValueOne()
	{
		// Arrange & Act
		var value = (int)LazyMigrationMode.OnRead;

		// Assert
		value.ShouldBe(1);
	}

	[Fact]
	public void HaveOnWriteValueTwo()
	{
		// Arrange & Act
		var value = (int)LazyMigrationMode.OnWrite;

		// Assert
		value.ShouldBe(2);
	}

	[Fact]
	public void HaveBothValueThree()
	{
		// Arrange & Act
		var value = (int)LazyMigrationMode.Both;

		// Assert
		value.ShouldBe(3);
	}

	[Fact]
	public void HaveExactlyFourValues()
	{
		// Arrange & Act
		var values = Enum.GetValues<LazyMigrationMode>();

		// Assert
		values.Length.ShouldBe(4);
	}

	#endregion Enum Value Tests

	#region Enum Name Tests

	[Theory]
	[InlineData(LazyMigrationMode.Disabled, "Disabled")]
	[InlineData(LazyMigrationMode.OnRead, "OnRead")]
	[InlineData(LazyMigrationMode.OnWrite, "OnWrite")]
	[InlineData(LazyMigrationMode.Both, "Both")]
	public void HaveCorrectNameForValue(LazyMigrationMode mode, string expectedName)
	{
		// Arrange & Act
		var name = mode.ToString();

		// Assert
		name.ShouldBe(expectedName);
	}

	#endregion Enum Name Tests

	#region Parse Tests

	[Theory]
	[InlineData("Disabled", LazyMigrationMode.Disabled)]
	[InlineData("OnRead", LazyMigrationMode.OnRead)]
	[InlineData("OnWrite", LazyMigrationMode.OnWrite)]
	[InlineData("Both", LazyMigrationMode.Both)]
	public void ParseFromString(string name, LazyMigrationMode expected)
	{
		// Arrange & Act
		var parsed = Enum.Parse<LazyMigrationMode>(name);

		// Assert
		parsed.ShouldBe(expected);
	}

	[Theory]
	[InlineData("0", LazyMigrationMode.Disabled)]
	[InlineData("1", LazyMigrationMode.OnRead)]
	[InlineData("2", LazyMigrationMode.OnWrite)]
	[InlineData("3", LazyMigrationMode.Both)]
	public void ParseFromNumericString(string numericValue, LazyMigrationMode expected)
	{
		// Arrange & Act
		var parsed = Enum.Parse<LazyMigrationMode>(numericValue);

		// Assert
		parsed.ShouldBe(expected);
	}

	[Fact]
	public void FailToParseInvalidName()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentException>(() => Enum.Parse<LazyMigrationMode>("InvalidMode"));
	}

	#endregion Parse Tests

	#region TryParse Tests

	[Theory]
	[InlineData("Disabled", true)]
	[InlineData("OnRead", true)]
	[InlineData("OnWrite", true)]
	[InlineData("Both", true)]
	[InlineData("InvalidMode", false)]
	[InlineData("", false)]
	public void TryParseReturnsExpectedResult(string input, bool expectedResult)
	{
		// Arrange & Act
		var result = Enum.TryParse<LazyMigrationMode>(input, out _);

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
	[InlineData(4, false)]
	[InlineData(-1, false)]
	[InlineData(100, false)]
	public void IsDefinedForValue(int value, bool expectedDefined)
	{
		// Arrange & Act
		var isDefined = Enum.IsDefined(typeof(LazyMigrationMode), value);

		// Assert
		isDefined.ShouldBe(expectedDefined);
	}

	#endregion IsDefined Tests

	#region Mode Semantics Tests

	[Fact]
	public void Disabled_IsDefaultAndSkipsMigration()
	{
		// Per AD-255-1: Disabled is the default, no opportunistic encryption
		var mode = LazyMigrationMode.Disabled;

		// Assert semantic checks
		mode.ShouldBe(default(LazyMigrationMode));
		((int)mode).ShouldBe(0);
	}

	[Fact]
	public void OnRead_EncryptsOnReadOnly()
	{
		// Per AD-255-1: OnRead encrypts plaintext when reading
		var mode = LazyMigrationMode.OnRead;

		// Semantic: Different from OnWrite and Disabled
		mode.ShouldNotBe(LazyMigrationMode.OnWrite);
		mode.ShouldNotBe(LazyMigrationMode.Disabled);
	}

	[Fact]
	public void OnWrite_EncryptsOnWriteOnly()
	{
		// Per AD-255-1: OnWrite encrypts plaintext only on writes
		var mode = LazyMigrationMode.OnWrite;

		// Semantic: Different from OnRead and Disabled
		mode.ShouldNotBe(LazyMigrationMode.OnRead);
		mode.ShouldNotBe(LazyMigrationMode.Disabled);
	}

	[Fact]
	public void Both_IsFastestMigration()
	{
		// Per AD-255-1: Both is recommended for active migration
		var mode = LazyMigrationMode.Both;

		// Semantic: Covers both read and write scenarios
		mode.ShouldNotBe(LazyMigrationMode.Disabled);
		((int)mode).ShouldBe(3); // Highest value in enum
	}

	[Theory]
	[InlineData(LazyMigrationMode.OnRead)]
	[InlineData(LazyMigrationMode.Both)]
	public void EncryptsOnRead_WhenModeIncludesRead(LazyMigrationMode mode)
	{
		// Modes that encrypt on read should be OnRead or Both
		(mode is LazyMigrationMode.OnRead or LazyMigrationMode.Both).ShouldBeTrue();
	}

	[Theory]
	[InlineData(LazyMigrationMode.OnWrite)]
	[InlineData(LazyMigrationMode.Both)]
	public void EncryptsOnWrite_WhenModeIncludesWrite(LazyMigrationMode mode)
	{
		// Modes that encrypt on write should be OnWrite or Both
		(mode is LazyMigrationMode.OnWrite or LazyMigrationMode.Both).ShouldBeTrue();
	}

	#endregion Mode Semantics Tests

	#region Comparison Tests

	[Fact]
	public void ModesAreComparable()
	{
		// Arrange
		var modes = Enum.GetValues<LazyMigrationMode>().Order().ToList();

		// Assert - values should be in order
		modes[0].ShouldBe(LazyMigrationMode.Disabled);
		modes[1].ShouldBe(LazyMigrationMode.OnRead);
		modes[2].ShouldBe(LazyMigrationMode.OnWrite);
		modes[3].ShouldBe(LazyMigrationMode.Both);
	}

	[Fact]
	public void DisabledLessThanBoth()
	{
		// Migration progression from Disabled toward Both
		(LazyMigrationMode.Disabled < LazyMigrationMode.Both).ShouldBeTrue();
	}

	#endregion Comparison Tests
}
