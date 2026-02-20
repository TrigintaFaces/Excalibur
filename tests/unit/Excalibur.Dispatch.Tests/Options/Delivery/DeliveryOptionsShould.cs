using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Delivery;

using DeliveryDeduplicationOptions = Excalibur.Dispatch.Options.Delivery.DeduplicationOptions;
using DeliveryInboxOptions = Excalibur.Dispatch.Options.Delivery.InboxOptions;
using DeliveryOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxOptions;

namespace Excalibur.Dispatch.Tests.Options.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DeliveryOptionsShould
{
	[Fact]
	public void DeduplicationOptions_HaveDefaults()
	{
		var opts = new DeliveryDeduplicationOptions();

		opts.DefaultExpiry.ShouldBe(TimeSpan.FromHours(24));
		opts.Enabled.ShouldBeTrue();
		opts.Strategy.ShouldBe(DeduplicationStrategy.MessageId);
		opts.MaxMemoryEntries.ShouldBe(10000);
		opts.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(5));
		opts.DeduplicationWindow.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void DeduplicationOptions_AllowSettingProperties()
	{
		var opts = new DeliveryDeduplicationOptions
		{
			DefaultExpiry = TimeSpan.FromHours(12),
			Enabled = false,
			Strategy = DeduplicationStrategy.ContentHash,
			MaxMemoryEntries = 5000,
			CleanupInterval = TimeSpan.FromMinutes(10),
			DeduplicationWindow = TimeSpan.FromMinutes(15),
		};

		opts.DefaultExpiry.ShouldBe(TimeSpan.FromHours(12));
		opts.Enabled.ShouldBeFalse();
		opts.Strategy.ShouldBe(DeduplicationStrategy.ContentHash);
		opts.MaxMemoryEntries.ShouldBe(5000);
		opts.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(10));
		opts.DeduplicationWindow.ShouldBe(TimeSpan.FromMinutes(15));
	}

	[Fact]
	public void DeliveryGuarantee_HaveExpectedValues()
	{
		DeliveryGuarantee.AtMostOnce.ShouldBe((DeliveryGuarantee)0);
		DeliveryGuarantee.AtLeastOnce.ShouldBe((DeliveryGuarantee)1);
	}

	[Fact]
	public void DeliveryGuaranteeOptions_HaveDefaults()
	{
		var opts = new DeliveryGuaranteeOptions();

		opts.Guarantee.ShouldBe(DeliveryGuarantee.AtLeastOnce);
		opts.EnableIdempotencyTracking.ShouldBeTrue();
		opts.IdempotencyKeyRetention.ShouldBe(TimeSpan.FromDays(7));
		opts.EnableAutomaticRetry.ShouldBeTrue();
	}

	[Fact]
	public void DeliveryGuaranteeOptions_AllowSettingProperties()
	{
		var opts = new DeliveryGuaranteeOptions
		{
			Guarantee = DeliveryGuarantee.AtMostOnce,
			EnableIdempotencyTracking = false,
			IdempotencyKeyRetention = TimeSpan.FromDays(1),
			EnableAutomaticRetry = false,
		};

		opts.Guarantee.ShouldBe(DeliveryGuarantee.AtMostOnce);
		opts.EnableIdempotencyTracking.ShouldBeFalse();
		opts.IdempotencyKeyRetention.ShouldBe(TimeSpan.FromDays(1));
		opts.EnableAutomaticRetry.ShouldBeFalse();
	}

	[Fact]
	public void EventStoreDispatcherOptions_HaveDefaults()
	{
		var opts = new EventStoreDispatcherOptions();

		opts.PollInterval.ShouldBe(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public void FilteredInvokerOptions_HaveDefaults()
	{
		var opts = new FilteredInvokerOptions();

		opts.EnableCaching.ShouldBeTrue();
		opts.IncludeMiddlewareOnFilterError.ShouldBeFalse();
		opts.MaxCachedEntries.ShouldBe(64);
	}

	[Fact]
	public void InboxOptions_HaveDefaults()
	{
		var opts = new DeliveryInboxOptions();

		opts.DuplicateBehavior.ShouldBe(SkipBehavior.Silent);
		opts.PerRunTotal.ShouldBe(1000);
		opts.QueueCapacity.ShouldBe(1000);
		opts.ProducerBatchSize.ShouldBe(100);
		opts.ConsumerBatchSize.ShouldBe(100);
		opts.MaxAttempts.ShouldBe(5);
		opts.DefaultMessageTimeToLive.ShouldBeNull();
		opts.Deduplication.ShouldNotBeNull();
		opts.ParallelProcessingDegree.ShouldBe(1);
		opts.EnableDynamicBatchSizing.ShouldBeFalse();
		opts.MinBatchSize.ShouldBe(10);
		opts.MaxBatchSize.ShouldBe(1000);
		opts.BatchProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		opts.EnableBatchDatabaseOperations.ShouldBeTrue();
	}

	[Fact]
	public void InboxOptions_Validate_ReturnsNullForValidDefaults()
	{
		var opts = new DeliveryInboxOptions();

		DeliveryInboxOptions.Validate(opts).ShouldBeNull();
	}

	[Fact]
	public void InboxOptions_Validate_ReturnsErrorForInvalidQueueCapacity()
	{
		var opts = new DeliveryInboxOptions { QueueCapacity = 0 };

		DeliveryInboxOptions.Validate(opts).ShouldNotBeNull();
	}

	[Fact]
	public void InboxOptions_Validate_ReturnsErrorWhenQueueCapacityLessThanProducerBatchSize()
	{
		var opts = new DeliveryInboxOptions { QueueCapacity = 10, ProducerBatchSize = 100 };

		DeliveryInboxOptions.Validate(opts).ShouldNotBeNull();
	}

	[Fact]
	public void InboxOptions_Validate_ReturnsErrorForDynamicBatchSizingWithInvalidMinMax()
	{
		var opts = new DeliveryInboxOptions
		{
			EnableDynamicBatchSizing = true,
			MinBatchSize = 500,
			MaxBatchSize = 100,
		};

		DeliveryInboxOptions.Validate(opts).ShouldNotBeNull();
	}

	[Fact]
	public void InboxOptions_Validate_ReturnsErrorForZeroBatchProcessingTimeout()
	{
		var opts = new DeliveryInboxOptions { BatchProcessingTimeout = TimeSpan.Zero };

		DeliveryInboxOptions.Validate(opts).ShouldNotBeNull();
	}

	[Fact]
	public void InMemoryDeduplicatorOptions_HaveDefaults()
	{
		var opts = new InMemoryDeduplicatorOptions();

		opts.MaxEntries.ShouldBe(100_000);
		opts.DefaultExpiry.ShouldBe(TimeSpan.FromHours(24));
		opts.EnableAutomaticCleanup.ShouldBeTrue();
		opts.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void InMemoryInboxOptions_HaveDefaults()
	{
		var opts = new InMemoryInboxOptions();

		opts.MaxEntries.ShouldBe(10_000);
		opts.EnableAutomaticCleanup.ShouldBeTrue();
		opts.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(5));
		opts.RetentionPeriod.ShouldBe(TimeSpan.FromHours(1));
		opts.CleanupBatchSize.ShouldBe(100);
	}

	[Fact]
	public void MessageEnvelopeOptions_HaveDefaults()
	{
		var opts = new MessageEnvelopeOptions();

		opts.ThreadLocalCacheSize.ShouldBe(16);
		opts.EnableTelemetry.ShouldBeFalse();
		opts.PoolContexts.ShouldBeTrue();
	}

	[Fact]
	public void MessageEnvelopePoolOptions_HaveDefaults()
	{
		var opts = new MessageEnvelopePoolOptions();

		opts.ThreadLocalCacheSize.ShouldBe(16);
		opts.EnableTelemetry.ShouldBeFalse();
	}

	[Fact]
	public void MiddlewareApplicabilityOptions_HaveDefaults()
	{
		var opts = new MiddlewareApplicabilityOptions();

		opts.IncludeOnError.ShouldBeFalse();
		opts.EnableCaching.ShouldBeTrue();
	}

	[Fact]
	public void OutboxDeliveryGuarantee_HaveExpectedValues()
	{
		OutboxDeliveryGuarantee.AtLeastOnce.ShouldBe((OutboxDeliveryGuarantee)0);
	}

	[Fact]
	public void OutboxOptions_HighThroughputPreset()
	{
		var opts = DeliveryOutboxOptions.HighThroughput();

		opts.PerRunTotal.ShouldBe(10000);
		opts.QueueCapacity.ShouldBe(10000);
		opts.ProducerBatchSize.ShouldBe(1000);
		opts.ConsumerBatchSize.ShouldBe(1000);
		opts.ParallelProcessingDegree.ShouldBe(8);
		opts.EnableDynamicBatchSizing.ShouldBeTrue();
		opts.DeliveryGuarantee.ShouldBe(OutboxDeliveryGuarantee.AtLeastOnce);
	}
}
