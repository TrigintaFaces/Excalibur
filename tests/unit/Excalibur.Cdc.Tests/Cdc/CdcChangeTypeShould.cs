// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Tests.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcChangeType"/> enum.
/// Tests the CDC change type enumeration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CdcChangeTypeShould : UnitTestBase
{
	[Fact]
	public void None_HasCorrectValue()
	{
		// Assert
		((int)CdcChangeType.None).ShouldBe(0);
	}

	[Fact]
	public void Insert_HasCorrectValue()
	{
		// Assert
		((int)CdcChangeType.Insert).ShouldBe(1);
	}

	[Fact]
	public void Update_HasCorrectValue()
	{
		// Assert
		((int)CdcChangeType.Update).ShouldBe(2);
	}

	[Fact]
	public void Delete_HasCorrectValue()
	{
		// Assert
		((int)CdcChangeType.Delete).ShouldBe(3);
	}

	[Fact]
	public void HasExpectedNumberOfValues()
	{
		// Arrange
		var values = Enum.GetValues<CdcChangeType>();

		// Assert
		values.Length.ShouldBe(10);
	}

	[Theory]
	[InlineData(CdcChangeType.None, "None")]
	[InlineData(CdcChangeType.Insert, "Insert")]
	[InlineData(CdcChangeType.Update, "Update")]
	[InlineData(CdcChangeType.Delete, "Delete")]
	public void ToString_ReturnsCorrectName(CdcChangeType changeType, string expectedName)
	{
		// Act
		var result = changeType.ToString();

		// Assert
		result.ShouldBe(expectedName);
	}

	[Theory]
	[InlineData("None", CdcChangeType.None)]
	[InlineData("Insert", CdcChangeType.Insert)]
	[InlineData("Update", CdcChangeType.Update)]
	[InlineData("Delete", CdcChangeType.Delete)]
	public void Parse_ReturnsCorrectValue(string name, CdcChangeType expectedValue)
	{
		// Act
		var result = Enum.Parse<CdcChangeType>(name);

		// Assert
		result.ShouldBe(expectedValue);
	}

	[Fact]
	public void DefaultValue_IsNone()
	{
		// This test documents that the default value is None (value 0)
		CdcChangeType defaultValue = default;
		defaultValue.ShouldBe(CdcChangeType.None);
	}
}
