// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.DataAccess;

/// <summary>
/// Unit tests for <see cref="ChangeType"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "DataAccess")]
[Trait("Priority", "0")]
public sealed class ChangeTypeShould
{
	#region Enum Value Tests

	[Fact]
	public void Insert_HasExpectedValue()
	{
		// Assert
		((int)ChangeType.Insert).ShouldBe(0);
	}

	[Fact]
	public void Update_HasExpectedValue()
	{
		// Assert
		((int)ChangeType.Update).ShouldBe(1);
	}

	[Fact]
	public void Delete_HasExpectedValue()
	{
		// Assert
		((int)ChangeType.Delete).ShouldBe(2);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void ContainsAllExpectedValues()
	{
		// Arrange
		var values = Enum.GetValues<ChangeType>();

		// Assert
		values.ShouldContain(ChangeType.Insert);
		values.ShouldContain(ChangeType.Update);
		values.ShouldContain(ChangeType.Delete);
	}

	[Fact]
	public void HasExactlyThreeValues()
	{
		// Arrange
		var values = Enum.GetValues<ChangeType>();

		// Assert
		values.Length.ShouldBe(3);
	}

	#endregion

	#region String Conversion Tests

	[Theory]
	[InlineData(ChangeType.Insert, "Insert")]
	[InlineData(ChangeType.Update, "Update")]
	[InlineData(ChangeType.Delete, "Delete")]
	public void ToString_ReturnsExpectedValue(ChangeType changeType, string expected)
	{
		// Act & Assert
		changeType.ToString().ShouldBe(expected);
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("Insert", ChangeType.Insert)]
	[InlineData("Update", ChangeType.Update)]
	[InlineData("Delete", ChangeType.Delete)]
	public void Parse_WithValidString_ReturnsExpectedValue(string value, ChangeType expected)
	{
		// Act
		var result = Enum.Parse<ChangeType>(value);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("insert")]
	[InlineData("INSERT")]
	[InlineData("update")]
	[InlineData("UPDATE")]
	[InlineData("delete")]
	[InlineData("DELETE")]
	public void Parse_CaseInsensitive_ReturnsExpectedValue(string value)
	{
		// Act
		var result = Enum.Parse<ChangeType>(value, ignoreCase: true);

		// Assert
		Enum.IsDefined(result).ShouldBeTrue();
	}

	[Fact]
	public void TryParse_WithInvalidString_ReturnsFalse()
	{
		// Act
		var success = Enum.TryParse<ChangeType>("Invalid", out _);

		// Assert
		success.ShouldBeFalse();
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsInsert()
	{
		// Arrange
		ChangeType changeType = default;

		// Assert
		changeType.ShouldBe(ChangeType.Insert);
	}

	#endregion
}
