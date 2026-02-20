// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb;
namespace Excalibur.Data.Tests.DynamoDb.Cdc;

/// <summary>
/// Unit tests for <see cref="DynamoDbStreamViewType"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Feature", "DynamoDb")]
public sealed class DynamoDbStreamViewTypeShould : UnitTestBase
{
	[Fact]
	public void HaveFourTypes()
	{
		// Act
		var values = Enum.GetValues<DynamoDbStreamViewType>();

		// Assert
		values.Length.ShouldBe(4);
	}

	[Fact]
	public void HaveKeysOnlyAsDefault()
	{
		// Assert
		DynamoDbStreamViewType defaultValue = default;
		defaultValue.ShouldBe(DynamoDbStreamViewType.KeysOnly);
	}

	[Theory]
	[InlineData(DynamoDbStreamViewType.KeysOnly, 0)]
	[InlineData(DynamoDbStreamViewType.NewImage, 1)]
	[InlineData(DynamoDbStreamViewType.OldImage, 2)]
	[InlineData(DynamoDbStreamViewType.NewAndOldImages, 3)]
	public void HaveCorrectUnderlyingValues(DynamoDbStreamViewType viewType, int expectedValue)
	{
		// Assert
		((int)viewType).ShouldBe(expectedValue);
	}

	[Theory]
	[InlineData("KeysOnly", DynamoDbStreamViewType.KeysOnly)]
	[InlineData("NewImage", DynamoDbStreamViewType.NewImage)]
	[InlineData("OldImage", DynamoDbStreamViewType.OldImage)]
	[InlineData("NewAndOldImages", DynamoDbStreamViewType.NewAndOldImages)]
	public void ParseFromString(string input, DynamoDbStreamViewType expected)
	{
		// Act
		var result = Enum.Parse<DynamoDbStreamViewType>(input);

		// Assert
		result.ShouldBe(expected);
	}
}
