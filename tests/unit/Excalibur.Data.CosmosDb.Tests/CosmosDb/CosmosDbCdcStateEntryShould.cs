// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb.Cdc;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for the <see cref="CosmosDbCdcStateEntry"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.3): CosmosDB unit tests.
/// Tests verify state entry properties and defaults.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait("Feature", "CDC")]
public sealed class CosmosDbCdcStateEntryShould
{
	#region Default Value Tests

	[Fact]
	public void ProcessorName_DefaultsToEmptyString()
	{
		// Arrange & Act
		var entry = new CosmosDbCdcStateEntry();

		// Assert
		entry.ProcessorName.ShouldBe(string.Empty);
	}

	[Fact]
	public void PositionData_DefaultsToEmptyString()
	{
		// Arrange & Act
		var entry = new CosmosDbCdcStateEntry();

		// Assert
		entry.PositionData.ShouldBe(string.Empty);
	}

	[Fact]
	public void UpdatedAt_DefaultsToUtcNow()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow.AddSeconds(-1);

		// Act
		var entry = new CosmosDbCdcStateEntry();

		// Assert
		var after = DateTimeOffset.UtcNow.AddSeconds(1);
		entry.UpdatedAt.ShouldBeInRange(before, after);
	}

	[Fact]
	public void EventCount_DefaultsToZero()
	{
		// Arrange & Act
		var entry = new CosmosDbCdcStateEntry();

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
		var entry = new CosmosDbCdcStateEntry
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
		typeof(CosmosDbCdcStateEntry).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(CosmosDbCdcStateEntry).IsPublic.ShouldBeTrue();
	}

	#endregion

	#region Property Type Tests

	[Fact]
	public void ProcessorName_IsString()
	{
		// Arrange
		var property = typeof(CosmosDbCdcStateEntry).GetProperty("ProcessorName");

		// Assert
		property.PropertyType.ShouldBe(typeof(string));
	}

	[Fact]
	public void PositionData_IsString()
	{
		// Arrange
		var property = typeof(CosmosDbCdcStateEntry).GetProperty("PositionData");

		// Assert
		property.PropertyType.ShouldBe(typeof(string));
	}

	[Fact]
	public void UpdatedAt_IsDateTimeOffset()
	{
		// Arrange
		var property = typeof(CosmosDbCdcStateEntry).GetProperty("UpdatedAt");

		// Assert
		property.PropertyType.ShouldBe(typeof(DateTimeOffset));
	}

	[Fact]
	public void EventCount_IsLong()
	{
		// Arrange
		var property = typeof(CosmosDbCdcStateEntry).GetProperty("EventCount");

		// Assert
		property.PropertyType.ShouldBe(typeof(long));
	}

	#endregion

	#region Property Count Tests

	[Fact]
	public void HasFourProperties()
	{
		// Arrange
		var properties = typeof(CosmosDbCdcStateEntry).GetProperties();

		// Assert
		properties.Length.ShouldBe(4);
	}

	#endregion
}
