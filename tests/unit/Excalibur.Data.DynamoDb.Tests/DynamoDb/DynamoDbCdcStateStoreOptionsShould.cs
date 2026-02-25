// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Cdc;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="DynamoDbCdcStateStoreOptions"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify CDC state store options defaults and validation.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "CDC")]
public sealed class DynamoDbCdcStateStoreOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void TableName_DefaultsToCdcState()
	{
		// Arrange & Act
		var options = new DynamoDbCdcStateStoreOptions();

		// Assert
		options.TableName.ShouldBe("cdc_state");
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void TableName_CanBeInitialized()
	{
		// Act
		var options = new DynamoDbCdcStateStoreOptions
		{
			TableName = "custom_cdc_state"
		};

		// Assert
		options.TableName.ShouldBe("custom_cdc_state");
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void Validate_Succeeds_WithDefaultOptions()
	{
		// Arrange
		var options = new DynamoDbCdcStateStoreOptions();

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Throws_WhenTableNameIsEmpty()
	{
		// Arrange
		var options = new DynamoDbCdcStateStoreOptions
		{
			TableName = ""
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("TableName");
	}

	[Fact]
	public void Validate_Throws_WhenTableNameIsWhitespace()
	{
		// Arrange
		var options = new DynamoDbCdcStateStoreOptions
		{
			TableName = "   "
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("TableName");
	}

	[Fact]
	public void Validate_Succeeds_WithCustomTableName()
	{
		// Arrange
		var options = new DynamoDbCdcStateStoreOptions
		{
			TableName = "my_custom_cdc_state"
		};

		// Act & Assert - Should not throw
		options.Validate();
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsSealed()
	{
		// Assert
		typeof(DynamoDbCdcStateStoreOptions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(DynamoDbCdcStateStoreOptions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
