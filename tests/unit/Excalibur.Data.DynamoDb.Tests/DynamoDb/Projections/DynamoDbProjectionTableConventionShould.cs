// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Projections;

namespace Excalibur.Data.DynamoDb.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DynamoDbProjectionTableConventionShould : UnitTestBase
{
	#region GetTableName<TProjection> Tests

	[Fact]
	public void ReturnTableNameFromOptions_Generic()
	{
		// Arrange
		var options = new DynamoDbProjectionStoreOptions { TableName = "my-projections" };

		// Act
		var result = DynamoDbProjectionTableConvention.GetTableName<SampleProjection>(options);

		// Assert
		result.ShouldBe("my-projections");
	}

	[Fact]
	public void ReturnDefaultTableName_Generic()
	{
		// Arrange
		var options = new DynamoDbProjectionStoreOptions();

		// Act
		var result = DynamoDbProjectionTableConvention.GetTableName<SampleProjection>(options);

		// Assert
		result.ShouldBe("Projections");
	}

	[Fact]
	public void ThrowWhenOptionsIsNull_Generic()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => DynamoDbProjectionTableConvention.GetTableName<SampleProjection>(null!));
	}

	#endregion

	#region GetTableName(options, projectionTypeName) Tests

	[Fact]
	public void ReturnTableNameFromOptions_ByName()
	{
		// Arrange
		var options = new DynamoDbProjectionStoreOptions { TableName = "custom-table" };

		// Act
		var result = DynamoDbProjectionTableConvention.GetTableName(options, "OrderSummary");

		// Assert
		result.ShouldBe("custom-table");
	}

	[Fact]
	public void ThrowWhenOptionsIsNull_ByName()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => DynamoDbProjectionTableConvention.GetTableName(null!, "OrderSummary"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowWhenProjectionTypeNameIsNullOrWhitespace(string? projectionTypeName)
	{
		// Arrange
		var options = new DynamoDbProjectionStoreOptions();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => DynamoDbProjectionTableConvention.GetTableName(options, projectionTypeName!));
	}

	#endregion

	private sealed class SampleProjection
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
	}
}
