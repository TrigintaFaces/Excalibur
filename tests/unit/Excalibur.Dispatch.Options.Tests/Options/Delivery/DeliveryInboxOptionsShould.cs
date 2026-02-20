// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Dispatch.Tests.Options.Delivery;

/// <summary>
/// Unit tests for <see cref="InboxOptions"/> in the Delivery namespace.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class DeliveryInboxOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_DuplicateBehavior_IsSilent()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		options.DuplicateBehavior.ShouldBe(SkipBehavior.Silent);
	}

	[Fact]
	public void Default_PerRunTotal_Is1000()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		options.PerRunTotal.ShouldBe(1000);
	}

	[Fact]
	public void Default_QueueCapacity_Is1000()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		options.QueueCapacity.ShouldBe(1000);
	}

	[Fact]
	public void Default_ProducerBatchSize_Is100()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		options.ProducerBatchSize.ShouldBe(100);
	}

	[Fact]
	public void Default_ConsumerBatchSize_Is100()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		options.ConsumerBatchSize.ShouldBe(100);
	}

	[Fact]
	public void Default_MaxAttempts_Is5()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		options.MaxAttempts.ShouldBe(5);
	}

	[Fact]
	public void Default_DefaultMessageTimeToLive_IsNull()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		options.DefaultMessageTimeToLive.ShouldBeNull();
	}

	[Fact]
	public void Default_Deduplication_IsNotNull()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		_ = options.Deduplication.ShouldNotBeNull();
	}

	[Fact]
	public void Default_ParallelProcessingDegree_IsOne()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		options.ParallelProcessingDegree.ShouldBe(1);
	}

	[Fact]
	public void Default_EnableDynamicBatchSizing_IsFalse()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		options.EnableDynamicBatchSizing.ShouldBeFalse();
	}

	[Fact]
	public void Default_MinBatchSize_IsTen()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		options.MinBatchSize.ShouldBe(10);
	}

	[Fact]
	public void Default_MaxBatchSize_Is1000()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		options.MaxBatchSize.ShouldBe(1000);
	}

	[Fact]
	public void Default_BatchProcessingTimeout_Is5Minutes()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		options.BatchProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Default_EnableBatchDatabaseOperations_IsTrue()
	{
		// Arrange & Act
		var options = new InboxOptions();

		// Assert
		options.EnableBatchDatabaseOperations.ShouldBeTrue();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void PerRunTotal_CanBeSet()
	{
		// Arrange
		var options = new InboxOptions();

		// Act
		options.PerRunTotal = 1000;

		// Assert
		options.PerRunTotal.ShouldBe(1000);
	}

	[Fact]
	public void QueueCapacity_CanBeSet()
	{
		// Arrange
		var options = new InboxOptions();

		// Act
		options.QueueCapacity = 500;

		// Assert
		options.QueueCapacity.ShouldBe(500);
	}

	[Fact]
	public void ParallelProcessingDegree_CanBeSet()
	{
		// Arrange
		var options = new InboxOptions();

		// Act
		options.ParallelProcessingDegree = 4;

		// Assert
		options.ParallelProcessingDegree.ShouldBe(4);
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void Validate_ReturnsNull_ForValidOptions()
	{
		// Arrange
		var options = new InboxOptions
		{
			PerRunTotal = 100,
			QueueCapacity = 100,
			ProducerBatchSize = 50,
			ConsumerBatchSize = 50,
			MaxAttempts = 3,
			ParallelProcessingDegree = 1,
			BatchProcessingTimeout = TimeSpan.FromMinutes(1),
		};

		// Act
		var result = InboxOptions.Validate(options);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void Validate_ReturnsError_WhenQueueCapacityIsZero()
	{
		// Arrange
		var options = new InboxOptions
		{
			PerRunTotal = 100,
			QueueCapacity = 0,
			ProducerBatchSize = 50,
			ConsumerBatchSize = 50,
			MaxAttempts = 3,
		};

		// Act
		var result = InboxOptions.Validate(options);

		// Assert
		_ = result.ShouldNotBeNull();
		result.ShouldContain("QueueCapacity");
	}

	[Fact]
	public void Validate_ReturnsError_WhenProducerBatchSizeExceedsQueueCapacity()
	{
		// Arrange
		var options = new InboxOptions
		{
			PerRunTotal = 100,
			QueueCapacity = 50,
			ProducerBatchSize = 100,
			ConsumerBatchSize = 50,
			MaxAttempts = 3,
			ParallelProcessingDegree = 1,
			BatchProcessingTimeout = TimeSpan.FromMinutes(1),
		};

		// Act
		var result = InboxOptions.Validate(options);

		// Assert
		_ = result.ShouldNotBeNull();
		result.ShouldContain("QueueCapacity");
	}

	[Fact]
	public void Validate_ReturnsError_WhenMinBatchSizeExceedsMaxBatchSize()
	{
		// Arrange
		var options = new InboxOptions
		{
			PerRunTotal = 100,
			QueueCapacity = 100,
			ProducerBatchSize = 50,
			ConsumerBatchSize = 50,
			MaxAttempts = 3,
			ParallelProcessingDegree = 1,
			EnableDynamicBatchSizing = true,
			MinBatchSize = 100,
			MaxBatchSize = 50,
			BatchProcessingTimeout = TimeSpan.FromMinutes(1),
		};

		// Act
		var result = InboxOptions.Validate(options);

		// Assert
		_ = result.ShouldNotBeNull();
		result.ShouldContain("MinBatchSize");
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighThroughput_HasLargeBatches()
	{
		// Act
		var options = new InboxOptions
		{
			PerRunTotal = 10000,
			QueueCapacity = 10000,
			ProducerBatchSize = 1000,
			ConsumerBatchSize = 1000,
			MaxAttempts = 3,
			ParallelProcessingDegree = 8,
		};

		// Assert
		options.ProducerBatchSize.ShouldBeGreaterThan(100);
		options.ParallelProcessingDegree.ShouldBeGreaterThan(1);
	}

	[Fact]
	public void Options_ForHighReliability_HasSmallBatches()
	{
		// Act
		var options = new InboxOptions
		{
			PerRunTotal = 100,
			QueueCapacity = 100,
			ProducerBatchSize = 10,
			ConsumerBatchSize = 10,
			MaxAttempts = 10,
			ParallelProcessingDegree = 1,
		};

		// Assert
		options.ProducerBatchSize.ShouldBeLessThan(100);
		options.MaxAttempts.ShouldBeGreaterThan(5);
	}

	#endregion
}
