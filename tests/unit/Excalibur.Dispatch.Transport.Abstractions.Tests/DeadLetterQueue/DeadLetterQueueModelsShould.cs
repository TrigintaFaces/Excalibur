using System.ComponentModel.DataAnnotations;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.DeadLetterQueue;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class DeadLetterQueueModelsShould
{
	[Fact]
	public void DeadLetterException_CapturesOriginalExceptionMetadata()
	{
		Exception inner;
		try
		{
			throw new InvalidOperationException("boom");
		}
		catch (Exception ex)
		{
			inner = ex;
		}

		var sut = new DeadLetterException("dead-lettered", inner);

		sut.Message.ShouldBe("dead-lettered");
		sut.ExceptionType.ShouldContain(nameof(InvalidOperationException));
		sut.OriginalStackTrace.ShouldNotBeNull();
		sut.StackTrace.ShouldBe(sut.OriginalStackTrace);
	}

	[Fact]
	public void DeadLetterException_DefaultConstructors_UseExpectedFallbackValues()
	{
		var defaultException = new DeadLetterException();
		var messageOnly = new DeadLetterException("oops");
		var nullableMessage = new DeadLetterException(null);

		defaultException.Message.ShouldBe(string.Empty);
		defaultException.ExceptionType.ShouldBe(string.Empty);
		defaultException.OriginalStackTrace.ShouldBeNull();

		messageOnly.Message.ShouldBe("oops");
		messageOnly.ExceptionType.ShouldBe(string.Empty);
		messageOnly.OriginalStackTrace.ShouldBeNull();

		nullableMessage.Message.ShouldBe(string.Empty);
	}

	[Fact]
	public void DeadLetterException_WithoutInnerException_UsesRuntimeStackTraceWhenThrown()
	{
		var captured = (DeadLetterException?)null;
		try
		{
			throw new DeadLetterException("thrown");
		}
		catch (DeadLetterException ex)
		{
			captured = ex;
		}

		captured.ShouldNotBeNull();
		captured.ExceptionType.ShouldBe(string.Empty);
		captured.OriginalStackTrace.ShouldBeNull();
		captured.StackTrace.ShouldNotBeNull();
	}

	[Fact]
	public void ReprocessResult_ComputesTotalCountAndSuccess()
	{
		var result = new ReprocessResult
		{
			SuccessCount = 4,
			FailureCount = 0,
			SkippedCount = 2,
			ProcessingTime = TimeSpan.FromSeconds(3),
		};

		result.TotalCount.ShouldBe(6);
		result.IsSuccess.ShouldBeTrue();

		result.FailureCount = 1;
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public void DeadLetterMessage_Serialization_OmitsOriginalEnvelope()
	{
		var message = new DeadLetterMessage
		{
			OriginalMessage = TransportMessage.FromString("payload"),
			OriginalEnvelope = new MessageEnvelope(),
			Reason = "processing-failed",
			DeliveryAttempts = 3,
			DeadLetteredAt = DateTimeOffset.UtcNow,
		};

		var json = JsonSerializer.Serialize(message);

		json.ShouldContain("processing-failed");
		json.ShouldNotContain("OriginalEnvelope");
	}

	[Fact]
	public void ReprocessOptions_FilterAndTransform_CanBeApplied()
	{
		var options = new ReprocessOptions
		{
			TargetQueue = "retry-queue",
			RetryDelay = TimeSpan.FromMilliseconds(250),
			RemoveFromDlq = false,
			ProcessInParallel = true,
			MaxDegreeOfParallelism = 8,
			MessageFilter = m => m.DeliveryAttempts >= 3,
			MessageTransform = m =>
			{
				m.Subject = "reprocessed";
				return m;
			},
		};

		var deadLetter = new DeadLetterMessage
		{
			OriginalMessage = TransportMessage.FromString("x"),
			DeliveryAttempts = 3,
		};

		options.MessageFilter!.Invoke(deadLetter).ShouldBeTrue();
		var transformed = options.MessageTransform!.Invoke(deadLetter.OriginalMessage);
		transformed.Subject.ShouldBe("reprocessed");
		options.TargetQueue.ShouldBe("retry-queue");
		options.MaxDegreeOfParallelism.ShouldBe(8);
	}

	[Fact]
	public void ReprocessOptions_ExposeOptionalPublishingFields()
	{
		var options = new ReprocessOptions
		{
			Priority = MessagePriority.Critical,
			TimeToLive = TimeSpan.FromMinutes(15),
			MaxMessages = 250,
		};

		options.Priority.ShouldBe(MessagePriority.Critical);
		options.TimeToLive.ShouldBe(TimeSpan.FromMinutes(15));
		options.MaxMessages.ShouldBe(250);
	}

	[Fact]
	public void ReprocessOptions_DataAnnotations_RejectOutOfRangeValues()
	{
		var options = new ReprocessOptions
		{
			MaxMessages = 0,
			MaxDegreeOfParallelism = 0,
		};

		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		isValid.ShouldBeFalse();
		results.Count.ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public void DeadLetterOptions_ExposeOperationalDefaults()
	{
		var options = new DeadLetterOptions();

		options.MaxDeliveryAttempts.ShouldBe(5);
		options.EnableAutomaticDeadLettering.ShouldBeTrue();
		options.EnableMonitoring.ShouldBeTrue();
		options.AlertThresholds.ShouldNotBeNull();

		options.AlertThresholds.MessageCountThreshold.ShouldBe(1000);
		options.AlertThresholds.QueueSizeThresholdInBytes.ShouldBe(536_870_912);
		options.AlertThresholds.FailureRateThreshold.ShouldBe(10.0);
	}

	[Fact]
	public void DeadLetterOptions_CanBeFullyConfigured()
	{
		var options = new DeadLetterOptions
		{
			DeadLetterQueueName = "orders-dlq",
			MaxDeliveryAttempts = 10,
			MessageRetentionPeriod = TimeSpan.FromDays(30),
			EnableAutomaticDeadLettering = true,
			IncludeStackTrace = false,
			MaxQueueSizeInBytes = 512_000,
			EnableMonitoring = true,
			MonitoringInterval = TimeSpan.FromMinutes(1),
			AlertThresholds = new DeadLetterAlertThresholds
			{
				MessageCountThreshold = 500,
				QueueSizeThresholdInBytes = 128_000,
				FailureRateThreshold = 4.5,
			},
		};

		options.DeadLetterQueueName.ShouldBe("orders-dlq");
		options.MaxDeliveryAttempts.ShouldBe(10);
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(30));
		options.IncludeStackTrace.ShouldBeFalse();
		options.MaxQueueSizeInBytes.ShouldBe(512_000);
		options.MonitoringInterval.ShouldBe(TimeSpan.FromMinutes(1));
		options.AlertThresholds.MessageCountThreshold.ShouldBe(500);
		options.AlertThresholds.QueueSizeThresholdInBytes.ShouldBe(128_000);
		options.AlertThresholds.FailureRateThreshold.ShouldBe(4.5);
	}

	[Fact]
	public void DeadLetterOptions_DataAnnotations_RejectOutOfRangeValues()
	{
		var options = new DeadLetterOptions
		{
			MaxDeliveryAttempts = 0,
			MaxQueueSizeInBytes = 0,
		};

		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();
		var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		isValid.ShouldBeFalse();
		results.Count.ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public void DeadLetterStatistics_AndReprocessFailure_HoldRuntimeData()
	{
		var stats = new DeadLetterStatistics
		{
			MessageCount = 10,
			AverageDeliveryAttempts = 2.5,
			OldestMessageAge = TimeSpan.FromHours(4),
			NewestMessageAge = TimeSpan.FromMinutes(1),
			SizeInBytes = 1024,
		};
		stats.ReasonBreakdown["validation"] = 6;
		stats.SourceBreakdown["orders"] = 4;
		stats.MessageTypeBreakdown["OrderPlaced"] = 3;

		var failure = new ReprocessFailure
		{
			Message = new DeadLetterMessage { OriginalMessage = TransportMessage.FromString("f"), Reason = "bad" },
			Reason = "transform failed",
			Exception = new InvalidOperationException("x"),
		};

		stats.ReasonBreakdown["validation"].ShouldBe(6);
		stats.SourceBreakdown["orders"].ShouldBe(4);
		stats.MessageTypeBreakdown["OrderPlaced"].ShouldBe(3);
		failure.Reason.ShouldBe("transform failed");
		failure.Message.OriginalMessage.ShouldNotBeNull();
	}
}
