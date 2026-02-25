// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="DataChangeType"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Feature", "Cdc")]
public sealed class DataChangeTypeShould : UnitTestBase
{
	[Fact]
	public void HaveUnknownAsDefaultValue()
	{
		// Assert
		((int)DataChangeType.Unknown).ShouldBe(0);
	}

	[Fact]
	public void HaveInsertValue()
	{
		// Assert
		((int)DataChangeType.Insert).ShouldBe(1);
	}

	[Fact]
	public void HaveUpdateValue()
	{
		// Assert
		((int)DataChangeType.Update).ShouldBe(2);
	}

	[Fact]
	public void HaveDeleteValue()
	{
		// Assert
		((int)DataChangeType.Delete).ShouldBe(3);
	}

	[Fact]
	public void HaveExpectedMemberCount()
	{
		// Assert
		Enum.GetValues<DataChangeType>().Length.ShouldBe(4);
	}

	[Theory]
	[InlineData("Unknown", DataChangeType.Unknown)]
	[InlineData("Insert", DataChangeType.Insert)]
	[InlineData("Update", DataChangeType.Update)]
	[InlineData("Delete", DataChangeType.Delete)]
	public void ParseFromString(string name, DataChangeType expected)
	{
		// Act
		var result = Enum.Parse<DataChangeType>(name);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void BeDefinedForAllValues()
	{
		// Assert
		Enum.IsDefined(DataChangeType.Unknown).ShouldBeTrue();
		Enum.IsDefined(DataChangeType.Insert).ShouldBeTrue();
		Enum.IsDefined(DataChangeType.Update).ShouldBeTrue();
		Enum.IsDefined(DataChangeType.Delete).ShouldBeTrue();
	}

	[Fact]
	public void DefaultToUnknown()
	{
		// Arrange & Act
		var defaultValue = default(DataChangeType);

		// Assert
		defaultValue.ShouldBe(DataChangeType.Unknown);
	}
}
