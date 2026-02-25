using Excalibur.Dispatch.Channels;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Metrics;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Validation.Context;

namespace Excalibur.Dispatch.Tests.Messaging.Enums;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessagingEnumsShould
{
	// --- MessageFlags (Flags enum, byte) ---

	[Fact]
	public void MessageFlags_HaveExpectedValues()
	{
		MessageFlags.None.ShouldBe((MessageFlags)0);
		MessageFlags.Compressed.ShouldBe((MessageFlags)1);
		MessageFlags.Encrypted.ShouldBe((MessageFlags)2);
		MessageFlags.Persistent.ShouldBe((MessageFlags)4);
		MessageFlags.HighPriority.ShouldBe((MessageFlags)8);
		MessageFlags.Validated.ShouldBe((MessageFlags)16);
	}

	[Fact]
	public void MessageFlags_SupportBitwiseCombination()
	{
		var combined = MessageFlags.Compressed | MessageFlags.Encrypted | MessageFlags.HighPriority;

		combined.HasFlag(MessageFlags.Compressed).ShouldBeTrue();
		combined.HasFlag(MessageFlags.Encrypted).ShouldBeTrue();
		combined.HasFlag(MessageFlags.HighPriority).ShouldBeTrue();
		combined.HasFlag(MessageFlags.Persistent).ShouldBeFalse();
	}

	// --- MissedExecutionBehavior ---

	[Fact]
	public void MissedExecutionBehavior_HaveExpectedValues()
	{
		MissedExecutionBehavior.SkipMissed.ShouldBe((MissedExecutionBehavior)0);
		MissedExecutionBehavior.ExecuteLatestMissed.ShouldBe((MissedExecutionBehavior)1);
		MissedExecutionBehavior.ExecuteAllMissed.ShouldBe((MissedExecutionBehavior)2);
		MissedExecutionBehavior.DisableSchedule.ShouldBe((MissedExecutionBehavior)3);
	}

	// --- PastScheduleBehavior ---

	[Fact]
	public void PastScheduleBehavior_HaveExpectedValues()
	{
		PastScheduleBehavior.Reject.ShouldBe((PastScheduleBehavior)0);
		PastScheduleBehavior.ExecuteImmediately.ShouldBe((PastScheduleBehavior)1);
	}

	// --- ChannelMessagePumpStatus ---

	[Fact]
	public void ChannelMessagePumpStatus_HaveExpectedValues()
	{
		ChannelMessagePumpStatus.NotStarted.ShouldBe((ChannelMessagePumpStatus)0);
		ChannelMessagePumpStatus.Starting.ShouldBe((ChannelMessagePumpStatus)1);
		ChannelMessagePumpStatus.Running.ShouldBe((ChannelMessagePumpStatus)2);
		ChannelMessagePumpStatus.Stopping.ShouldBe((ChannelMessagePumpStatus)3);
		ChannelMessagePumpStatus.Stopped.ShouldBe((ChannelMessagePumpStatus)4);
		ChannelMessagePumpStatus.Faulted.ShouldBe((ChannelMessagePumpStatus)5);
	}

	// --- ChannelMode ---

	[Fact]
	public void ChannelMode_HaveExpectedValues()
	{
		ChannelMode.Unbounded.ShouldBe((ChannelMode)0);
		ChannelMode.Bounded.ShouldBe((ChannelMode)1);
	}

	// --- DeadLetterReason ---

	[Fact]
	public void DeadLetterReason_HaveExpectedValues()
	{
		DeadLetterReason.MaxRetriesExceeded.ShouldBe((DeadLetterReason)0);
		DeadLetterReason.CircuitBreakerOpen.ShouldBe((DeadLetterReason)1);
		DeadLetterReason.DeserializationFailed.ShouldBe((DeadLetterReason)2);
		DeadLetterReason.HandlerNotFound.ShouldBe((DeadLetterReason)3);
		DeadLetterReason.ValidationFailed.ShouldBe((DeadLetterReason)4);
		DeadLetterReason.ManualRejection.ShouldBe((DeadLetterReason)5);
		DeadLetterReason.MessageExpired.ShouldBe((DeadLetterReason)6);
		DeadLetterReason.AuthorizationFailed.ShouldBe((DeadLetterReason)7);
		DeadLetterReason.UnhandledException.ShouldBe((DeadLetterReason)8);
		DeadLetterReason.PoisonMessage.ShouldBe((DeadLetterReason)9);
		DeadLetterReason.Unknown.ShouldBe((DeadLetterReason)99);
	}

	// --- BackoffStrategy ---

	[Fact]
	public void BackoffStrategy_HaveExpectedValues()
	{
		BackoffStrategy.Fixed.ShouldBe((BackoffStrategy)0);
		BackoffStrategy.Linear.ShouldBe((BackoffStrategy)1);
		BackoffStrategy.Exponential.ShouldBe((BackoffStrategy)2);
		BackoffStrategy.ExponentialWithJitter.ShouldBe((BackoffStrategy)3);
		BackoffStrategy.Fibonacci.ShouldBe((BackoffStrategy)4);
	}

	// --- CircuitState ---

	[Fact]
	public void CircuitState_HaveExpectedValues()
	{
		CircuitState.Closed.ShouldBe((CircuitState)0);
		CircuitState.Open.ShouldBe((CircuitState)1);
		CircuitState.HalfOpen.ShouldBe((CircuitState)2);
	}

	// --- MetricType ---

	[Fact]
	public void MetricType_HaveExpectedValues()
	{
		MetricType.Counter.ShouldBe((MetricType)0);
		MetricType.Gauge.ShouldBe((MetricType)1);
		MetricType.Histogram.ShouldBe((MetricType)2);
		MetricType.Summary.ShouldBe((MetricType)3);
	}

	// --- PoolHealth ---

	[Fact]
	public void PoolHealth_HaveExpectedValues()
	{
		PoolHealth.Healthy.ShouldBe((PoolHealth)0);
		PoolHealth.Warning.ShouldBe((PoolHealth)1);
		PoolHealth.Critical.ShouldBe((PoolHealth)2);
	}

	// --- PreWarmStrategy ---

	[Fact]
	public void PreWarmStrategy_HaveExpectedValues()
	{
		PreWarmStrategy.ThreadLocal.ShouldBe((PreWarmStrategy)0);
		PreWarmStrategy.Global.ShouldBe((PreWarmStrategy)1);
		PreWarmStrategy.Balanced.ShouldBe((PreWarmStrategy)2);
	}

	// --- ValidationMode ---

	[Fact]
	public void ValidationMode_HaveExpectedValues()
	{
		ValidationMode.Strict.ShouldBe((ValidationMode)0);
		ValidationMode.Lenient.ShouldBe((ValidationMode)1);
	}

	// --- ValidationSeverity ---

	[Fact]
	public void ValidationSeverity_HaveExpectedValues()
	{
		ValidationSeverity.Info.ShouldBe((ValidationSeverity)0);
		ValidationSeverity.Warning.ShouldBe((ValidationSeverity)1);
		ValidationSeverity.Error.ShouldBe((ValidationSeverity)2);
		ValidationSeverity.Critical.ShouldBe((ValidationSeverity)3);
	}

	// --- CloudEventMode ---

	[Fact]
	public void CloudEventMode_HaveExpectedValues()
	{
		CloudEventMode.Structured.ShouldBe((CloudEventMode)0);
		CloudEventMode.Binary.ShouldBe((CloudEventMode)1);
	}

	// --- SchemaCompatibilityMode ---

	[Fact]
	public void SchemaCompatibilityMode_HaveExpectedValues()
	{
		SchemaCompatibilityMode.None.ShouldBe((SchemaCompatibilityMode)0);
		SchemaCompatibilityMode.Forward.ShouldBe((SchemaCompatibilityMode)1);
		SchemaCompatibilityMode.Backward.ShouldBe((SchemaCompatibilityMode)2);
		SchemaCompatibilityMode.Full.ShouldBe((SchemaCompatibilityMode)3);
	}
}
