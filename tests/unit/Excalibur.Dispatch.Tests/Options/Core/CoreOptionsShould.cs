using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CoreOptionsShould
{
	[Fact]
	public void CompressionOptions_HaveDefaults()
	{
		var opts = new CompressionOptions();

		opts.Enabled.ShouldBeFalse();
		opts.CompressionType.ShouldBe(CompressionType.Gzip);
		opts.CompressionLevel.ShouldBe(6);
		opts.MinimumSizeThreshold.ShouldBe(1024);
	}

	[Fact]
	public void CompressionOptions_AllowSettingProperties()
	{
		var opts = new CompressionOptions
		{
			Enabled = true,
			CompressionType = CompressionType.Brotli,
			CompressionLevel = 9,
			MinimumSizeThreshold = 512,
		};

		opts.Enabled.ShouldBeTrue();
		opts.CompressionType.ShouldBe(CompressionType.Brotli);
		opts.CompressionLevel.ShouldBe(9);
		opts.MinimumSizeThreshold.ShouldBe(512);
	}

	[Fact]
	public void DeadLetterOptions_HaveDefaults()
	{
		var opts = new DeadLetterOptions();

		opts.MaxAttempts.ShouldBe(3);
		opts.QueueName.ShouldBe("deadletter");
		opts.PreserveMetadata.ShouldBeTrue();
		opts.IncludeExceptionDetails.ShouldBeTrue();
		opts.EnableRecovery.ShouldBeFalse();
		opts.RecoveryInterval.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void DeadLetterOptions_AllowSettingProperties()
	{
		var opts = new DeadLetterOptions
		{
			MaxAttempts = 5,
			QueueName = "custom-dlq",
			PreserveMetadata = false,
			IncludeExceptionDetails = false,
			EnableRecovery = true,
			RecoveryInterval = TimeSpan.FromMinutes(30),
		};

		opts.MaxAttempts.ShouldBe(5);
		opts.QueueName.ShouldBe("custom-dlq");
		opts.PreserveMetadata.ShouldBeFalse();
		opts.IncludeExceptionDetails.ShouldBeFalse();
		opts.EnableRecovery.ShouldBeTrue();
		opts.RecoveryInterval.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void EncryptionOptions_HaveDefaults()
	{
		var opts = new EncryptionOptions();

		opts.Enabled.ShouldBeFalse();
		opts.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
		opts.Key.ShouldBeNull();
		opts.KeyDerivation.ShouldBeNull();
		opts.EnableKeyRotation.ShouldBeFalse();
	}

	[Fact]
	public void EncryptionOptions_AllowSettingProperties()
	{
		var key = new byte[] { 1, 2, 3 };
		var opts = new EncryptionOptions
		{
			Enabled = true,
			Algorithm = EncryptionAlgorithm.Aes128Gcm,
			Key = key,
			KeyDerivation = new KeyDerivationOptions(),
			EnableKeyRotation = true,
		};

		opts.Enabled.ShouldBeTrue();
		opts.Algorithm.ShouldBe(EncryptionAlgorithm.Aes128Gcm);
		opts.Key.ShouldBe(key);
		opts.KeyDerivation.ShouldNotBeNull();
		opts.EnableKeyRotation.ShouldBeTrue();
	}

	[Fact]
	public void PipelineOptions_HaveDefaults()
	{
		var opts = new PipelineOptions();

		opts.MaxConcurrency.ShouldBe(Environment.ProcessorCount * 2);
		opts.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		opts.EnableParallelProcessing.ShouldBeTrue();
		opts.StopOnFirstError.ShouldBeFalse();
		opts.BufferSize.ShouldBe(1000);
		opts.ApplicableMessageKinds.ShouldBe(MessageKinds.Action | MessageKinds.Event | MessageKinds.Document);
	}

	[Fact]
	public void PipelineOptions_AllowSettingProperties()
	{
		var opts = new PipelineOptions
		{
			MaxConcurrency = 8,
			DefaultTimeout = TimeSpan.FromSeconds(60),
			EnableParallelProcessing = false,
			StopOnFirstError = true,
			BufferSize = 500,
			ApplicableMessageKinds = MessageKinds.Action,
		};

		opts.MaxConcurrency.ShouldBe(8);
		opts.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(60));
		opts.EnableParallelProcessing.ShouldBeFalse();
		opts.StopOnFirstError.ShouldBeTrue();
		opts.BufferSize.ShouldBe(500);
		opts.ApplicableMessageKinds.ShouldBe(MessageKinds.Action);
	}

	[Fact]
	public void HealthCheckOptions_HaveDefaults()
	{
		var opts = new HealthCheckOptions();

		opts.Enabled.ShouldBeFalse();
		opts.Timeout.ShouldBe(TimeSpan.FromSeconds(10));
		opts.Interval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void InMemoryBusOptions_HaveDefaults()
	{
		var opts = new InMemoryBusOptions();

		opts.MaxQueueLength.ShouldBe(1000);
		opts.PreserveOrder.ShouldBeTrue();
		opts.ProcessingDelay.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void InMemoryBusOptions_AllowSettingProperties()
	{
		var opts = new InMemoryBusOptions
		{
			MaxQueueLength = 500,
			PreserveOrder = false,
			ProcessingDelay = TimeSpan.FromMilliseconds(50),
		};

		opts.MaxQueueLength.ShouldBe(500);
		opts.PreserveOrder.ShouldBeFalse();
		opts.ProcessingDelay.ShouldBe(TimeSpan.FromMilliseconds(50));
	}

	[Fact]
	public void LoggingOptions_HaveDefaults()
	{
		var opts = new LoggingOptions();

		opts.EnhancedLogging.ShouldBeFalse();
		opts.IncludeCorrelationIds.ShouldBeTrue();
		opts.IncludeExecutionContext.ShouldBeTrue();
	}

	[Fact]
	public void LoggingOptions_AllowSettingProperties()
	{
		var opts = new LoggingOptions
		{
			EnhancedLogging = true,
			IncludeCorrelationIds = false,
			IncludeExecutionContext = false,
		};

		opts.EnhancedLogging.ShouldBeTrue();
		opts.IncludeCorrelationIds.ShouldBeFalse();
		opts.IncludeExecutionContext.ShouldBeFalse();
	}

	[Fact]
	public void MetricsOptions_HaveDefaults()
	{
		var opts = new MetricsOptions();

		opts.Enabled.ShouldBeFalse();
		opts.ExportInterval.ShouldBe(TimeSpan.FromSeconds(30));
		opts.CustomTags.ShouldBeEmpty();
	}

	[Fact]
	public void MetricsOptions_AllowSettingProperties()
	{
		var opts = new MetricsOptions
		{
			Enabled = true,
			ExportInterval = TimeSpan.FromSeconds(10),
		};
		opts.CustomTags["env"] = "test";

		opts.Enabled.ShouldBeTrue();
		opts.ExportInterval.ShouldBe(TimeSpan.FromSeconds(10));
		opts.CustomTags["env"].ShouldBe("test");
	}

	[Fact]
	public void TimeoutOptions_HaveDefaults()
	{
		var opts = new TimeoutOptions();

		opts.Enabled.ShouldBeFalse();
		opts.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		opts.MessageTypeTimeouts.ShouldBeEmpty();
		opts.ThrowOnTimeout.ShouldBeTrue();
	}

	[Fact]
	public void TimeoutOptions_AllowSettingProperties()
	{
		var opts = new TimeoutOptions
		{
			Enabled = true,
			DefaultTimeout = TimeSpan.FromSeconds(60),
			ThrowOnTimeout = false,
		};
		opts.MessageTypeTimeouts["OrderCommand"] = TimeSpan.FromSeconds(120);

		opts.Enabled.ShouldBeTrue();
		opts.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(60));
		opts.ThrowOnTimeout.ShouldBeFalse();
		opts.MessageTypeTimeouts["OrderCommand"].ShouldBe(TimeSpan.FromSeconds(120));
	}

	[Fact]
	public void TracingOptions_HaveDefaults()
	{
		var opts = new TracingOptions();

		opts.Enabled.ShouldBeFalse();
		opts.SamplingRatio.ShouldBe(1.0);
		opts.IncludeSensitiveData.ShouldBeFalse();
	}

	[Fact]
	public void TracingOptions_AllowSettingProperties()
	{
		var opts = new TracingOptions
		{
			Enabled = true,
			SamplingRatio = 0.5,
			IncludeSensitiveData = true,
		};

		opts.Enabled.ShouldBeTrue();
		opts.SamplingRatio.ShouldBe(0.5);
		opts.IncludeSensitiveData.ShouldBeTrue();
	}

	[Fact]
	public void SerializationOptions_HaveDefaults()
	{
		var opts = new SerializationOptions();

		opts.ShouldNotBeNull();
	}

	[Fact]
	public void MessageRoutingOptions_HaveDefaults()
	{
		var opts = new MessageRoutingOptions();

		opts.ShouldNotBeNull();
	}

	[Fact]
	public void MiddlewareRegistrationOptions_HaveDefaults()
	{
		var opts = new MiddlewareRegistrationOptions();

		opts.ShouldNotBeNull();
	}

	[Fact]
	public void MultiTransportOptions_HaveDefaults()
	{
		var opts = new MultiTransportOptions();

		opts.ShouldNotBeNull();
	}

	[Fact]
	public void DispatchProfileOptions_HaveDefaults()
	{
		var opts = new DispatchProfileOptions();

		opts.ShouldNotBeNull();
	}

	[Fact]
	public void JsonSerializationOptions_HaveDefaults()
	{
		var opts = new JsonSerializationOptions();

		opts.ShouldNotBeNull();
	}

	[Fact]
	public void MessageBusHealthCheckOptions_HaveDefaults()
	{
		var opts = new MessageBusHealthCheckOptions();

		opts.ShouldNotBeNull();
	}

	[Fact]
	public void KeyDerivationOptions_HaveDefaults()
	{
		var opts = new KeyDerivationOptions();

		opts.ShouldNotBeNull();
	}
}
