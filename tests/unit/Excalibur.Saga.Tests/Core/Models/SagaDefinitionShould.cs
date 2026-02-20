// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Models;

namespace Excalibur.Saga.Tests.Core.Models;

/// <summary>
/// Unit tests for <see cref="SagaDefinition{TData}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaDefinitionShould
{
	#region Default Values Tests

	[Fact]
	public void HaveEmptyNameByDefault()
	{
		// Arrange & Act
		var definition = new SagaDefinition<TestSagaData>();

		// Assert
		definition.Name.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultVersionOf1_0()
	{
		// Arrange & Act
		var definition = new SagaDefinition<TestSagaData>();

		// Assert
		definition.Version.ShouldBe("1.0");
	}

	[Fact]
	public void HaveNullDescriptionByDefault()
	{
		// Arrange & Act
		var definition = new SagaDefinition<TestSagaData>();

		// Assert
		definition.Description.ShouldBeNull();
	}

	[Fact]
	public void HaveEmptyStepsCollectionByDefault()
	{
		// Arrange & Act
		var definition = new SagaDefinition<TestSagaData>();

		// Assert
		definition.Steps.ShouldBeEmpty();
	}

	[Fact]
	public void HaveDefaultTimeoutOf30Minutes()
	{
		// Arrange & Act
		var definition = new SagaDefinition<TestSagaData>();

		// Assert
		definition.Timeout.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void HaveDefaultRetentionPeriodOf7Days()
	{
		// Arrange & Act
		var definition = new SagaDefinition<TestSagaData>();

		// Assert
		definition.RetentionPeriod.ShouldBe(TimeSpan.FromDays(7));
	}

	[Fact]
	public void HaveEnableCachingTrueByDefault()
	{
		// Arrange & Act
		var definition = new SagaDefinition<TestSagaData>();

		// Assert
		definition.EnableCaching.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultCacheTtlOf5Minutes()
	{
		// Arrange & Act
		var definition = new SagaDefinition<TestSagaData>();

		// Assert
		definition.CacheTtl.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void HaveEmptyMetadataDictionaryByDefault()
	{
		// Arrange & Act
		var definition = new SagaDefinition<TestSagaData>();

		// Assert
		definition.Metadata.ShouldBeEmpty();
	}

	#endregion Default Values Tests

	#region Property Setting Tests

	[Fact]
	public void AllowNameToBeSet()
	{
		// Arrange
		var definition = new SagaDefinition<TestSagaData>();

		// Act
		definition.Name = "OrderProcessingSaga";

		// Assert
		definition.Name.ShouldBe("OrderProcessingSaga");
	}

	[Fact]
	public void AllowVersionToBeSet()
	{
		// Arrange
		var definition = new SagaDefinition<TestSagaData>();

		// Act
		definition.Version = "2.1";

		// Assert
		definition.Version.ShouldBe("2.1");
	}

	[Fact]
	public void AllowDescriptionToBeSet()
	{
		// Arrange
		var definition = new SagaDefinition<TestSagaData>();

		// Act
		definition.Description = "Processes orders through the system";

		// Assert
		definition.Description.ShouldBe("Processes orders through the system");
	}

	[Fact]
	public void AllowTimeoutToBeSet()
	{
		// Arrange
		var definition = new SagaDefinition<TestSagaData>();

		// Act
		definition.Timeout = TimeSpan.FromHours(1);

		// Assert
		definition.Timeout.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void AllowRetentionPeriodToBeSet()
	{
		// Arrange
		var definition = new SagaDefinition<TestSagaData>();

		// Act
		definition.RetentionPeriod = TimeSpan.FromDays(30);

		// Assert
		definition.RetentionPeriod.ShouldBe(TimeSpan.FromDays(30));
	}

	[Fact]
	public void AllowEnableCachingToBeSet()
	{
		// Arrange
		var definition = new SagaDefinition<TestSagaData>();

		// Act
		definition.EnableCaching = false;

		// Assert
		definition.EnableCaching.ShouldBeFalse();
	}

	[Fact]
	public void AllowCacheTtlToBeSet()
	{
		// Arrange
		var definition = new SagaDefinition<TestSagaData>();

		// Act
		definition.CacheTtl = TimeSpan.FromMinutes(10);

		// Assert
		definition.CacheTtl.ShouldBe(TimeSpan.FromMinutes(10));
	}

	#endregion Property Setting Tests

	#region Metadata Tests

	[Fact]
	public void AllowMetadataToBeAdded()
	{
		// Arrange
		var definition = new SagaDefinition<TestSagaData>();

		// Act
		definition.Metadata["Priority"] = "High";
		definition.Metadata["Department"] = "Sales";

		// Assert
		definition.Metadata.Count.ShouldBe(2);
		definition.Metadata["Priority"].ShouldBe("High");
		definition.Metadata["Department"].ShouldBe("Sales");
	}

	[Fact]
	public void AllowMetadataToBeRemoved()
	{
		// Arrange
		var definition = new SagaDefinition<TestSagaData>();
		definition.Metadata["Priority"] = "High";

		// Act
		definition.Metadata.Remove("Priority");

		// Assert
		definition.Metadata.ShouldNotContainKey("Priority");
	}

	[Fact]
	public void UseOrdinalComparisonForMetadataKeys()
	{
		// Arrange
		var definition = new SagaDefinition<TestSagaData>();

		// Act
		definition.Metadata["Key"] = "value1";
		definition.Metadata["KEY"] = "value2";

		// Assert - Should have two distinct keys due to ordinal comparison
		definition.Metadata.Count.ShouldBe(2);
	}

	#endregion Metadata Tests

	/// <summary>
	/// Test saga data class.
	/// </summary>
	private sealed class TestSagaData
	{
		public string OrderId { get; set; } = string.Empty;
	}
}
