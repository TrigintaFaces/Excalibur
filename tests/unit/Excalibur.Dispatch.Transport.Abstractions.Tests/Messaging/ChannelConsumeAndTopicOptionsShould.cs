using System.ComponentModel.DataAnnotations;
using System.Threading.Channels;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class ChannelConsumeAndTopicOptionsShould
{
	[Fact]
	public void DefaultOptions_AreInitializedForGeneralConsumption()
	{
		var options = ChannelConsumeOptions.Default;

		options.Buffer.ShouldNotBeNull();
		options.Acknowledgment.ShouldNotBeNull();
		options.Retry.ShouldNotBeNull();
		options.CompleteChannelOnStop.ShouldBeTrue();
		options.PreserveOrdering.ShouldBeFalse();
	}

	[Fact]
	public void HighThroughputPreset_OptimizesConcurrencyAndBatching()
	{
		var options = ChannelConsumeOptions.HighThroughput;

		options.Buffer.MaxConcurrency.ShouldBe(Environment.ProcessorCount * 2);
		options.Buffer.PrefetchCount.ShouldBe(100);
		options.Acknowledgment.UseBatchAcknowledgment.ShouldBeTrue();
		options.Acknowledgment.AcknowledgmentBatchSize.ShouldBe(50);
		options.Retry.EnableAutoRetry.ShouldBeFalse();
	}

	[Fact]
	public void OrderedPreset_EnforcesSingleThreadedOrderedBehavior()
	{
		var options = ChannelConsumeOptions.Ordered;

		options.Buffer.MaxConcurrency.ShouldBe(1);
		options.Buffer.PrefetchCount.ShouldBe(1);
		options.PreserveOrdering.ShouldBeTrue();
		options.Acknowledgment.UseBatchAcknowledgment.ShouldBeFalse();
	}

	[Fact]
	public void ReliablePreset_EnablesRetryAndDeadLettering()
	{
		var options = ChannelConsumeOptions.Reliable;

		options.Acknowledgment.AutoAcknowledge.ShouldBeFalse();
		options.Retry.EnableAutoRetry.ShouldBeTrue();
		options.Retry.MaxRetryAttempts.ShouldBe(5);
		options.Retry.UseExponentialBackoff.ShouldBeTrue();
		options.Retry.DeadLetterStrategy.ShouldBe(DeadLetterStrategy.MoveToDeadLetterQueue);
	}

	[Fact]
	public void OrderingSelector_CanBeConfigured()
	{
		var options = new ChannelConsumeOptions
		{
			PreserveOrdering = true,
			OrderingKeySelector = envelope => envelope.CorrelationId
		};
		var envelope = new MessageEnvelope { CorrelationId = "corr-1" };

		options.OrderingKeySelector!(envelope).ShouldBe("corr-1");
	}

	[Fact]
	public void ChannelBufferOptions_CanSwitchToUnboundedMode()
	{
		var buffer = new ChannelBufferOptions
		{
			ChannelOptions = new UnboundedChannelOptions { SingleReader = true, SingleWriter = true },
			ChannelCapacity = 2048,
			FullMode = BoundedChannelFullMode.DropOldest,
			MaxConcurrency = 16,
			PrefetchCount = 32,
			BatchSize = 20,
			MaxWaitTime = TimeSpan.FromMilliseconds(250)
		};

		buffer.ChannelOptions.ShouldNotBeNull();
		buffer.ChannelOptions!.SingleReader.ShouldBeTrue();
		buffer.ChannelOptions.SingleWriter.ShouldBeTrue();
		buffer.FullMode.ShouldBe(BoundedChannelFullMode.DropOldest);
		buffer.ChannelCapacity.ShouldBe(2048);
		buffer.AllowSynchronousContinuations.ShouldBeFalse();
		buffer.MaxConcurrency.ShouldBe(16);
		buffer.PrefetchCount.ShouldBe(32);
		buffer.BatchSize.ShouldBe(20);
		buffer.MaxWaitTime.ShouldBe(TimeSpan.FromMilliseconds(250));
	}

	[Fact]
	public void ChannelAcknowledgmentOptions_Defaults_AreOperational()
	{
		var options = new ChannelAcknowledgmentOptions();

		options.AutoAcknowledge.ShouldBeTrue();
		options.VisibilityTimeout.ShouldBeNull();
		options.UseBatchAcknowledgment.ShouldBeTrue();
		options.AcknowledgmentBatchSize.ShouldBe(10);
		options.AcknowledgmentBatchTimeout.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void ChannelRetryOptions_Defaults_AreResilient()
	{
		var options = new ChannelRetryOptions();

		options.EnableAutoRetry.ShouldBeTrue();
		options.MaxRetryAttempts.ShouldBe(3);
		options.RetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
		options.UseExponentialBackoff.ShouldBeTrue();
		options.DeadLetterStrategy.ShouldBe(DeadLetterStrategy.MoveToDeadLetterQueue);
	}

	[Fact]
	public void DataAnnotations_Reject_Invalid_Buffer_And_Acknowledgment_Values()
	{
		var buffer = new ChannelBufferOptions
		{
			ChannelCapacity = 0,
			MaxConcurrency = 0,
			PrefetchCount = 0,
			BatchSize = 0,
		};
		var ack = new ChannelAcknowledgmentOptions
		{
			AcknowledgmentBatchSize = 0,
		};

		var bufferValidation = Validator.TryValidateObject(
			buffer,
			new ValidationContext(buffer),
			new List<ValidationResult>(),
			validateAllProperties: true);
		var ackValidation = Validator.TryValidateObject(
			ack,
			new ValidationContext(ack),
			new List<ValidationResult>(),
			validateAllProperties: true);

		bufferValidation.ShouldBeFalse();
		ackValidation.ShouldBeFalse();
	}

	[Fact]
	public void TopicOptions_HoldProvisioningConfiguration()
	{
		var options = new TopicOptions
		{
			MaxSizeInMB = 1024,
			DefaultMessageTimeToLive = TimeSpan.FromHours(1),
			EnableDeduplication = true,
			DuplicateDetectionWindow = TimeSpan.FromMinutes(5),
			RequiresDuplicateDetection = true,
			SupportOrdering = true,
			EnablePartitioning = true,
			EnableBatchedOperations = true,
			Status = EntityStatus.Active,
		};
		options.Properties["partition-count"] = 8;

		options.Status.ShouldBe(EntityStatus.Active);
		options.Properties["partition-count"].ShouldBe(8);
	}
}
