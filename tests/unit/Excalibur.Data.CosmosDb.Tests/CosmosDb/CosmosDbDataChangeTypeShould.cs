// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb;
namespace Excalibur.Data.Tests.CosmosDb.Cdc;

/// <summary>
/// Unit tests for <see cref="CosmosDbDataChangeType"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Feature", "CosmosDb")]
public sealed class CosmosDbDataChangeTypeShould : UnitTestBase
{
	[Fact]
	public void HaveThreeTypes()
	{
		// Act
		var values = Enum.GetValues<CosmosDbDataChangeType>();

		// Assert
		values.Length.ShouldBe(3);
	}

	[Fact]
	public void HaveInsertAsDefault()
	{
		// Assert
		CosmosDbDataChangeType defaultValue = default;
		defaultValue.ShouldBe(CosmosDbDataChangeType.Insert);
	}

	[Theory]
	[InlineData(CosmosDbDataChangeType.Insert, 0)]
	[InlineData(CosmosDbDataChangeType.Update, 1)]
	[InlineData(CosmosDbDataChangeType.Delete, 2)]
	public void HaveCorrectUnderlyingValues(CosmosDbDataChangeType changeType, int expectedValue)
	{
		// Assert
		((int)changeType).ShouldBe(expectedValue);
	}

	[Theory]
	[InlineData("Insert", CosmosDbDataChangeType.Insert)]
	[InlineData("Update", CosmosDbDataChangeType.Update)]
	[InlineData("Delete", CosmosDbDataChangeType.Delete)]
	public void ParseFromString(string input, CosmosDbDataChangeType expected)
	{
		// Act
		var result = Enum.Parse<CosmosDbDataChangeType>(input);

		// Assert
		result.ShouldBe(expected);
	}
}
