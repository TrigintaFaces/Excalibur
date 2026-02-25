// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Cdc;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for the <see cref="DynamoDbCdcStateEntry"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify state entry properties and defaults.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "CDC")]
public sealed class DynamoDbCdcStateEntryShould
{
	#region Default Value Tests

	[Fact]
	public void ProcessorName_DefaultsToEmptyString()
	{
		// Arrange & Act
		var entry = new DynamoDbCdcStateEntry();

		// Assert
		entry.ProcessorName.ShouldBe(string.Empty);
	}

	[Fact]
	public void PositionData_DefaultsToEmptyString()
	{
		// Arrange & Act
		var entry = new DynamoDbCdcStateEntry();

		// Assert
		entry.PositionData.ShouldBe(string.Empty);
	}

	[Fact]
	public void UpdatedAt_DefaultsToUtcNow()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow.AddSeconds(-1);

		// Act
		var entry = new DynamoDbCdcStateEntry();

		// Assert
		var after = DateTimeOffset.UtcNow.AddSeconds(1);
		entry.UpdatedAt.ShouldBeInRange(before, after);
	}

	[Fact]
	public void EventCount_DefaultsToZero()
	{
		// Arrange & Act
		var entry = new DynamoDbCdcStateEntry();

		// Assert
		entry.EventCount.ShouldBe(0L);
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange
		var updatedAt = DateTimeOffset.UtcNow;

		// Act
		var entry = new DynamoDbCdcStateEntry
		{
			ProcessorName = "test-processor",
			PositionData = "YWJjMTIz",
			UpdatedAt = updatedAt,
			EventCount = 1000
		};

		// Assert
		entry.ProcessorName.ShouldBe("test-processor");
		entry.PositionData.ShouldBe("YWJjMTIz");
		entry.UpdatedAt.ShouldBe(updatedAt);
		entry.EventCount.ShouldBe(1000L);
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsSealed()
	{
		// Assert
		typeof(DynamoDbCdcStateEntry).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(DynamoDbCdcStateEntry).IsPublic.ShouldBeTrue();
	}

	#endregion

	#region Property Type Tests

	[Fact]
	public void ProcessorName_IsString()
	{
		// Arrange
		var property = typeof(DynamoDbCdcStateEntry).GetProperty("ProcessorName");

		// Assert
		property.PropertyType.ShouldBe(typeof(string));
	}

	[Fact]
	public void PositionData_IsString()
	{
		// Arrange
		var property = typeof(DynamoDbCdcStateEntry).GetProperty("PositionData");

		// Assert
		property.PropertyType.ShouldBe(typeof(string));
	}

	[Fact]
	public void UpdatedAt_IsDateTimeOffset()
	{
		// Arrange
		var property = typeof(DynamoDbCdcStateEntry).GetProperty("UpdatedAt");

		// Assert
		property.PropertyType.ShouldBe(typeof(DateTimeOffset));
	}

	[Fact]
	public void EventCount_IsLong()
	{
		// Arrange
		var property = typeof(DynamoDbCdcStateEntry).GetProperty("EventCount");

		// Assert
		property.PropertyType.ShouldBe(typeof(long));
	}

	#endregion

	#region Property Count Tests

	[Fact]
	public void HasFourProperties()
	{
		// Arrange
		var properties = typeof(DynamoDbCdcStateEntry).GetProperties();

		// Assert
		properties.Length.ShouldBe(4);
	}

	#endregion
}
