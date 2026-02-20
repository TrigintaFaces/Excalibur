// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DeliveryOptionsShould
{
	// --- DeduplicationOptions ---

	[Fact]
	public void DeduplicationOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new DeduplicationOptions();

		// Assert
		options.DefaultExpiry.ShouldBe(TimeSpan.FromHours(24));
		options.Enabled.ShouldBeTrue();
		options.Strategy.ShouldBe(DeduplicationStrategy.MessageId);
		options.MaxMemoryEntries.ShouldBe(10000);
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(5));
		options.DeduplicationWindow.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void DeduplicationOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new DeduplicationOptions
		{
			DefaultExpiry = TimeSpan.FromHours(12),
			Enabled = false,
			Strategy = DeduplicationStrategy.ContentHash,
			MaxMemoryEntries = 5000,
			CleanupInterval = TimeSpan.FromMinutes(10),
			DeduplicationWindow = TimeSpan.FromMinutes(10),
		};

		// Assert
		options.DefaultExpiry.ShouldBe(TimeSpan.FromHours(12));
		options.Enabled.ShouldBeFalse();
		options.Strategy.ShouldBe(DeduplicationStrategy.ContentHash);
		options.MaxMemoryEntries.ShouldBe(5000);
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(10));
		options.DeduplicationWindow.ShouldBe(TimeSpan.FromMinutes(10));
	}

	// --- DeliveryGuarantee & DeliveryGuaranteeOptions ---

	[Fact]
	public void DeliveryGuarantee_HaveExpectedValues()
	{
		// Assert
		DeliveryGuarantee.AtMostOnce.ShouldBe((DeliveryGuarantee)0);
		DeliveryGuarantee.AtLeastOnce.ShouldBe((DeliveryGuarantee)1);
	}

	[Fact]
	public void DeliveryGuaranteeOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new DeliveryGuaranteeOptions();

		// Assert
		options.Guarantee.ShouldBe(DeliveryGuarantee.AtLeastOnce);
		options.EnableIdempotencyTracking.ShouldBeTrue();
		options.IdempotencyKeyRetention.ShouldBe(TimeSpan.FromDays(7));
		options.EnableAutomaticRetry.ShouldBeTrue();
	}

	[Fact]
	public void DeliveryGuaranteeOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new DeliveryGuaranteeOptions
		{
			Guarantee = DeliveryGuarantee.AtMostOnce,
			EnableIdempotencyTracking = false,
			IdempotencyKeyRetention = TimeSpan.FromDays(1),
			EnableAutomaticRetry = false,
		};

		// Assert
		options.Guarantee.ShouldBe(DeliveryGuarantee.AtMostOnce);
		options.EnableIdempotencyTracking.ShouldBeFalse();
		options.IdempotencyKeyRetention.ShouldBe(TimeSpan.FromDays(1));
		options.EnableAutomaticRetry.ShouldBeFalse();
	}

	// --- InboxOptions ---

	[Fact]
	public void InboxOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new InboxOptions();

		// Assert
		options.DuplicateBehavior.ShouldBe(SkipBehavior.Silent);
		options.PerRunTotal.ShouldBe(1000);
		options.QueueCapacity.ShouldBe(1000);
		options.ProducerBatchSize.ShouldBe(100);
		options.ConsumerBatchSize.ShouldBe(100);
		options.MaxAttempts.ShouldBe(5);
		options.DefaultMessageTimeToLive.ShouldBeNull();
		options.Deduplication.ShouldNotBeNull();
		options.ParallelProcessingDegree.ShouldBe(1);
		options.EnableDynamicBatchSizing.ShouldBeFalse();
		options.MinBatchSize.ShouldBe(10);
		options.MaxBatchSize.ShouldBe(1000);
		options.BatchProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		options.EnableBatchDatabaseOperations.ShouldBeTrue();
	}

	[Fact]
	public void InboxOptions_Validate_ReturnsNull_WhenValid()
	{
		// Arrange
		var options = new InboxOptions();

		// Act
		var result = InboxOptions.Validate(options);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void InboxOptions_Validate_ThrowsOnNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => InboxOptions.Validate(null!));
	}

	[Fact]
	public void InboxOptions_Validate_DetectsZeroQueueCapacity()
	{
		// Arrange
		var options = new InboxOptions { QueueCapacity = 0 };

		// Act
		var result = InboxOptions.Validate(options);

		// Assert
		result.ShouldNotBeNull();
		result.ShouldContain("QueueCapacity");
	}

	[Fact]
	public void InboxOptions_Validate_DetectsQueueSmallerThanBatch()
	{
		// Arrange
		var options = new InboxOptions { QueueCapacity = 10, ProducerBatchSize = 100 };

		// Act
		var result = InboxOptions.Validate(options);

		// Assert
		result.ShouldNotBeNull();
		result.ShouldContain("QueueCapacity");
	}

	[Fact]
	public void InboxOptions_Validate_DetectsDynamicBatchMinGreaterThanMax()
	{
		// Arrange
		var options = new InboxOptions
		{
			EnableDynamicBatchSizing = true,
			MinBatchSize = 500,
			MaxBatchSize = 100,
		};

		// Act
		var result = InboxOptions.Validate(options);

		// Assert
		result.ShouldNotBeNull();
		result.ShouldContain("MinBatchSize");
	}

	[Fact]
	public void InboxOptions_Validate_DetectsZeroBatchTimeout()
	{
		// Arrange
		var options = new InboxOptions { BatchProcessingTimeout = TimeSpan.Zero };

		// Act
		var result = InboxOptions.Validate(options);

		// Assert
		result.ShouldNotBeNull();
		result.ShouldContain("BatchProcessingTimeout");
	}

	// --- InMemoryDeduplicatorOptions ---

	[Fact]
	public void InMemoryDeduplicatorOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new InMemoryDeduplicatorOptions();

		// Assert
		options.MaxEntries.ShouldBe(100_000);
		options.DefaultExpiry.ShouldBe(TimeSpan.FromHours(24));
		options.EnableAutomaticCleanup.ShouldBeTrue();
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(30));
	}

	// --- InMemoryInboxOptions ---

	[Fact]
	public void InMemoryInboxOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new InMemoryInboxOptions();

		// Assert
		options.MaxEntries.ShouldBe(10_000);
		options.EnableAutomaticCleanup.ShouldBeTrue();
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(5));
		options.RetentionPeriod.ShouldBe(TimeSpan.FromHours(1));
		options.CleanupBatchSize.ShouldBe(100);
	}

	// --- OutboxDeliveryGuarantee ---

	[Fact]
	public void OutboxDeliveryGuarantee_HaveExpectedValues()
	{
		// Assert
		OutboxDeliveryGuarantee.AtLeastOnce.ShouldBe((OutboxDeliveryGuarantee)0);
		OutboxDeliveryGuarantee.MinimizedWindow.ShouldBe((OutboxDeliveryGuarantee)1);
		OutboxDeliveryGuarantee.TransactionalWhenApplicable.ShouldBe((OutboxDeliveryGuarantee)2);
	}

	[Fact]
	public void OutboxDeliveryGuarantee_HaveThreeValues()
	{
		// Act
		var values = Enum.GetValues<OutboxDeliveryGuarantee>();

		// Assert
		values.Length.ShouldBe(3);
	}

	// --- OutboxOptions ---

	[Fact]
	public void OutboxOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new OutboxOptions();

		// Assert
		options.PerRunTotal.ShouldBe(1000);
		options.QueueCapacity.ShouldBe(1000);
		options.ProducerBatchSize.ShouldBe(100);
		options.ConsumerBatchSize.ShouldBe(100);
		options.MaxAttempts.ShouldBe(5);
		options.DefaultMessageTimeToLive.ShouldBeNull();
		options.ParallelProcessingDegree.ShouldBe(1);
		options.EnableDynamicBatchSizing.ShouldBeFalse();
		options.MinBatchSize.ShouldBe(10);
		options.MaxBatchSize.ShouldBe(1000);
		options.BatchProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		options.EnableBatchDatabaseOperations.ShouldBeTrue();
		options.DeliveryGuarantee.ShouldBe(OutboxDeliveryGuarantee.AtLeastOnce);
	}

	[Fact]
	public void OutboxOptions_HighThroughput_Preset()
	{
		// Act
		var options = OutboxOptions.HighThroughput();

		// Assert
		options.PerRunTotal.ShouldBe(10000);
		options.ProducerBatchSize.ShouldBe(1000);
		options.ConsumerBatchSize.ShouldBe(1000);
		options.ParallelProcessingDegree.ShouldBe(8);
		options.EnableDynamicBatchSizing.ShouldBeTrue();
		options.DeliveryGuarantee.ShouldBe(OutboxDeliveryGuarantee.AtLeastOnce);
	}

	[Fact]
	public void OutboxOptions_Balanced_Preset()
	{
		// Act
		var options = OutboxOptions.Balanced();

		// Assert
		options.PerRunTotal.ShouldBe(1000);
		options.ProducerBatchSize.ShouldBe(100);
		options.ParallelProcessingDegree.ShouldBe(4);
		options.EnableDynamicBatchSizing.ShouldBeFalse();
	}

	[Fact]
	public void OutboxOptions_HighReliability_Preset()
	{
		// Act
		var options = OutboxOptions.HighReliability();

		// Assert
		options.PerRunTotal.ShouldBe(100);
		options.ProducerBatchSize.ShouldBe(10);
		options.ParallelProcessingDegree.ShouldBe(1);
		options.DeliveryGuarantee.ShouldBe(OutboxDeliveryGuarantee.MinimizedWindow);
	}

	[Fact]
	public void OutboxOptions_WithBatchSize_CreatesNewInstance()
	{
		// Arrange
		var original = OutboxOptions.HighThroughput();

		// Act
		var modified = original.WithBatchSize(500);

		// Assert
		modified.ProducerBatchSize.ShouldBe(500);
		modified.ConsumerBatchSize.ShouldBe(500);
		original.ProducerBatchSize.ShouldBe(1000); // unchanged
	}

	[Fact]
	public void OutboxOptions_WithBatchSize_SetsConsumerSeparately()
	{
		// Act
		var options = OutboxOptions.Balanced().WithBatchSize(200, 50);

		// Assert
		options.ProducerBatchSize.ShouldBe(200);
		options.ConsumerBatchSize.ShouldBe(50);
	}

	[Fact]
	public void OutboxOptions_WithParallelDegree_CreatesNewInstance()
	{
		// Arrange
		var original = OutboxOptions.Balanced();

		// Act
		var modified = original.WithParallelDegree(16);

		// Assert
		modified.ParallelProcessingDegree.ShouldBe(16);
		original.ParallelProcessingDegree.ShouldBe(4); // unchanged
	}

	[Fact]
	public void OutboxOptions_WithDeliveryGuarantee_CreatesNewInstance()
	{
		// Act
		var options = OutboxOptions.Balanced()
			.WithDeliveryGuarantee(OutboxDeliveryGuarantee.TransactionalWhenApplicable);

		// Assert
		options.DeliveryGuarantee.ShouldBe(OutboxDeliveryGuarantee.TransactionalWhenApplicable);
	}

	[Fact]
	public void OutboxOptions_WithMaxAttempts_CreatesNewInstance()
	{
		// Act
		var options = OutboxOptions.Balanced().WithMaxAttempts(7);

		// Assert
		options.MaxAttempts.ShouldBe(7);
	}

	[Fact]
	public void OutboxOptions_WithTimeout_CreatesNewInstance()
	{
		// Act
		var options = OutboxOptions.HighThroughput().WithTimeout(TimeSpan.FromMinutes(15));

		// Assert
		options.BatchProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(15));
	}

	[Fact]
	public void OutboxOptions_Validate_ReturnsNull_WhenValid()
	{
		// Act
		var result = OutboxOptions.Validate(new OutboxOptions());

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void OutboxOptions_Validate_ThrowsOnNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => OutboxOptions.Validate(null!));
	}

	[Fact]
	public void OutboxOptions_Validate_DetectsZeroQueueCapacity()
	{
		// Arrange
		var options = new OutboxOptions { QueueCapacity = 0 };

		// Act
		var result = OutboxOptions.Validate(options);

		// Assert
		result.ShouldNotBeNull();
		result.ShouldContain("QueueCapacity");
	}

	// --- MessageEnvelopeOptions ---

	[Fact]
	public void MessageEnvelopeOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new MessageEnvelopeOptions();

		// Assert
		options.ThreadLocalCacheSize.ShouldBe(16);
		options.EnableTelemetry.ShouldBeFalse();
		options.PoolContexts.ShouldBeTrue();
	}

	// --- FilteredInvokerOptions ---

	[Fact]
	public void FilteredInvokerOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new FilteredInvokerOptions();

		// Assert
		options.EnableCaching.ShouldBeTrue();
		options.IncludeMiddlewareOnFilterError.ShouldBeFalse();
		options.MaxCachedEntries.ShouldBe(64);
	}
}
