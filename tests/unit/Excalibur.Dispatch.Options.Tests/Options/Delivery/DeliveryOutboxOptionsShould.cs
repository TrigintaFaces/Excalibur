// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Dispatch.Tests.Options.Delivery;

/// <summary>
/// Unit tests for <see cref="OutboxOptions"/> in the Delivery namespace.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class DeliveryOutboxOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_PerRunTotal_Is1000()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.PerRunTotal.ShouldBe(1000);
	}

	[Fact]
	public void Default_QueueCapacity_Is1000()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.QueueCapacity.ShouldBe(1000);
	}

	[Fact]
	public void Default_ParallelProcessingDegree_IsOne()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.ParallelProcessingDegree.ShouldBe(1);
	}

	[Fact]
	public void Default_EnableDynamicBatchSizing_IsFalse()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.EnableDynamicBatchSizing.ShouldBeFalse();
	}

	[Fact]
	public void Default_MinBatchSize_IsTen()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.MinBatchSize.ShouldBe(10);
	}

	[Fact]
	public void Default_MaxBatchSize_Is1000()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.MaxBatchSize.ShouldBe(1000);
	}

	[Fact]
	public void Default_BatchProcessingTimeout_Is5Minutes()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.BatchProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Default_EnableBatchDatabaseOperations_IsTrue()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.EnableBatchDatabaseOperations.ShouldBeTrue();
	}

	[Fact]
	public void Default_DeliveryGuarantee_IsAtLeastOnce()
	{
		// Arrange & Act
		var options = new OutboxOptions();

		// Assert
		options.DeliveryGuarantee.ShouldBe(OutboxDeliveryGuarantee.AtLeastOnce);
	}

	#endregion

	#region Static Factory Tests

	[Fact]
	public void HighThroughput_ReturnsConfiguredOptions()
	{
		// Act
		var options = OutboxOptions.HighThroughput();

		// Assert
		options.PerRunTotal.ShouldBe(10000);
		options.QueueCapacity.ShouldBe(10000);
		options.ProducerBatchSize.ShouldBe(1000);
		options.ConsumerBatchSize.ShouldBe(1000);
		options.ParallelProcessingDegree.ShouldBe(8);
		options.EnableDynamicBatchSizing.ShouldBeTrue();
	}

	[Fact]
	public void Balanced_ReturnsConfiguredOptions()
	{
		// Act
		var options = OutboxOptions.Balanced();

		// Assert
		options.PerRunTotal.ShouldBe(1000);
		options.QueueCapacity.ShouldBe(1000);
		options.ProducerBatchSize.ShouldBe(100);
		options.ConsumerBatchSize.ShouldBe(100);
		options.ParallelProcessingDegree.ShouldBe(4);
	}

	[Fact]
	public void HighReliability_ReturnsConfiguredOptions()
	{
		// Act
		var options = OutboxOptions.HighReliability();

		// Assert
		options.PerRunTotal.ShouldBe(100);
		options.QueueCapacity.ShouldBe(100);
		options.ProducerBatchSize.ShouldBe(10);
		options.ConsumerBatchSize.ShouldBe(10);
		options.ParallelProcessingDegree.ShouldBe(1);
		options.DeliveryGuarantee.ShouldBe(OutboxDeliveryGuarantee.MinimizedWindow);
	}

	#endregion

	#region Fluent Customization Tests

	[Fact]
	public void WithBatchSize_ReturnsNewInstanceWithUpdatedBatchSize()
	{
		// Arrange
		var original = OutboxOptions.Balanced();

		// Act
		var modified = original.WithBatchSize(500);

		// Assert
		modified.ProducerBatchSize.ShouldBe(500);
		modified.ConsumerBatchSize.ShouldBe(500);
		original.ProducerBatchSize.ShouldBe(100); // Original unchanged
	}

	[Fact]
	public void WithParallelDegree_ReturnsNewInstanceWithUpdatedParallelism()
	{
		// Arrange
		var original = OutboxOptions.Balanced();

		// Act
		var modified = original.WithParallelDegree(8);

		// Assert
		modified.ParallelProcessingDegree.ShouldBe(8);
		original.ParallelProcessingDegree.ShouldBe(4); // Original unchanged
	}

	[Fact]
	public void WithDeliveryGuarantee_ReturnsNewInstanceWithUpdatedGuarantee()
	{
		// Arrange
		var original = OutboxOptions.Balanced();

		// Act
		var modified = original.WithDeliveryGuarantee(OutboxDeliveryGuarantee.MinimizedWindow);

		// Assert
		modified.DeliveryGuarantee.ShouldBe(OutboxDeliveryGuarantee.MinimizedWindow);
		original.DeliveryGuarantee.ShouldBe(OutboxDeliveryGuarantee.AtLeastOnce); // Original unchanged
	}

	[Fact]
	public void WithMaxAttempts_ReturnsNewInstanceWithUpdatedAttempts()
	{
		// Arrange
		var original = OutboxOptions.Balanced();

		// Act
		var modified = original.WithMaxAttempts(7);

		// Assert
		modified.MaxAttempts.ShouldBe(7);
		original.MaxAttempts.ShouldBe(5); // Original unchanged
	}

	[Fact]
	public void WithTimeout_ReturnsNewInstanceWithUpdatedTimeout()
	{
		// Arrange
		var original = OutboxOptions.Balanced();

		// Act
		var modified = original.WithTimeout(TimeSpan.FromMinutes(15));

		// Assert
		modified.BatchProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(15));
		original.BatchProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(5)); // Original unchanged
	}

	[Fact]
	public void FluentMethods_CanBeChained()
	{
		// Act
		var options = OutboxOptions.HighThroughput()
			.WithBatchSize(500)
			.WithParallelDegree(4)
			.WithMaxAttempts(5);

		// Assert
		options.ProducerBatchSize.ShouldBe(500);
		options.ParallelProcessingDegree.ShouldBe(4);
		options.MaxAttempts.ShouldBe(5);
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void Validate_ReturnsNull_ForValidOptions()
	{
		// Arrange
		var options = OutboxOptions.Balanced();

		// Act
		var result = OutboxOptions.Validate(options);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void Validate_ReturnsError_WhenQueueCapacityIsZero()
	{
		// Arrange
		var options = new OutboxOptions
		{
			PerRunTotal = 100,
			QueueCapacity = 0,
			ProducerBatchSize = 50,
			ConsumerBatchSize = 50,
			MaxAttempts = 3,
		};

		// Act
		var result = OutboxOptions.Validate(options);

		// Assert
		_ = result.ShouldNotBeNull();
		result.ShouldContain("QueueCapacity");
	}

	[Fact]
	public void Validate_ReturnsError_WhenBatchProcessingTimeoutIsZero()
	{
		// Arrange
		var options = OutboxOptions.Balanced();
		options.BatchProcessingTimeout = TimeSpan.Zero;

		// Act
		var result = OutboxOptions.Validate(options);

		// Assert
		_ = result.ShouldNotBeNull();
		result.ShouldContain("BatchProcessingTimeout");
	}

	#endregion
}
