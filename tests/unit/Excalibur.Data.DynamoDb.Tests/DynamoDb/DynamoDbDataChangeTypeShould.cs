// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb;
namespace Excalibur.Data.Tests.DynamoDb.Cdc;

/// <summary>
/// Unit tests for <see cref="DynamoDbDataChangeType"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Feature", "DynamoDb")]
public sealed class DynamoDbDataChangeTypeShould : UnitTestBase
{
	[Fact]
	public void HaveThreeTypes()
	{
		// Act
		var values = Enum.GetValues<DynamoDbDataChangeType>();

		// Assert
		values.Length.ShouldBe(3);
	}

	[Fact]
	public void HaveInsertAsDefault()
	{
		// Assert
		DynamoDbDataChangeType defaultValue = default;
		defaultValue.ShouldBe(DynamoDbDataChangeType.Insert);
	}

	[Theory]
	[InlineData(DynamoDbDataChangeType.Insert, 0)]
	[InlineData(DynamoDbDataChangeType.Modify, 1)]
	[InlineData(DynamoDbDataChangeType.Remove, 2)]
	public void HaveCorrectUnderlyingValues(DynamoDbDataChangeType changeType, int expectedValue)
	{
		// Assert
		((int)changeType).ShouldBe(expectedValue);
	}

	[Theory]
	[InlineData("Insert", DynamoDbDataChangeType.Insert)]
	[InlineData("Modify", DynamoDbDataChangeType.Modify)]
	[InlineData("Remove", DynamoDbDataChangeType.Remove)]
	public void ParseFromString(string input, DynamoDbDataChangeType expected)
	{
		// Act
		var result = Enum.Parse<DynamoDbDataChangeType>(input);

		// Assert
		result.ShouldBe(expected);
	}
}
