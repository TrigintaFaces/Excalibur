// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;
using Excalibur.Dispatch.Options.Channels;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.DataAnnotations;

/// <summary>
/// Verifies DataAnnotation validation on Channel, CloudEvent, and Core Options classes.
/// Sprint 564 S564.56: Channel + CloudEvent + Core Options DataAnnotation coverage.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ChannelAndCloudEventAnnotationsShould
{
	private static bool TryValidate(object instance, out ICollection<ValidationResult> results)
	{
		results = new List<ValidationResult>();
		return Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
	}

	#region SpinWaitOptions

	[Fact]
	public void SpinWaitOptions_Succeed_WithDefaults()
	{
		var options = new SpinWaitOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void SpinWaitOptions_Fail_WhenSpinCountIsZero()
	{
		var options = new SpinWaitOptions { SpinCount = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(SpinWaitOptions.SpinCount)));
	}

	[Fact]
	public void SpinWaitOptions_Fail_WhenDelayIsNegative()
	{
		var options = new SpinWaitOptions { DelayMilliseconds = -1 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(SpinWaitOptions.DelayMilliseconds)));
	}

	[Fact]
	public void SpinWaitOptions_Fail_WhenSpinIterationsIsZero()
	{
		var options = new SpinWaitOptions { SpinIterations = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(SpinWaitOptions.SpinIterations)));
	}

	#endregion

	#region BoundedDispatchChannelOptions

	[Fact]
	public void BoundedChannel_Succeed_WithDefaults()
	{
		var options = new BoundedDispatchChannelOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void BoundedChannel_Fail_WhenSpinCountIsZero()
	{
		var options = new BoundedDispatchChannelOptions { SpinCount = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(BoundedDispatchChannelOptions.SpinCount)));
	}

	#endregion

	#region ChannelMessagePumpOptions

	[Fact]
	public void ChannelMessagePump_Succeed_WithDefaults()
	{
		var options = new ChannelMessagePumpOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void ChannelMessagePump_Fail_WhenCapacityIsZero()
	{
		var options = new ChannelMessagePumpOptions { Capacity = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(ChannelMessagePumpOptions.Capacity)));
	}

	[Fact]
	public void ChannelMessagePump_Fail_WhenBatchSizeIsZero()
	{
		var options = new ChannelMessagePumpOptions { BatchSize = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(ChannelMessagePumpOptions.BatchSize)));
	}

	#endregion

	#region CloudEventBatchOptions

	[Fact]
	public void CloudEventBatch_Succeed_WithDefaults()
	{
		var options = new CloudEventBatchOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void CloudEventBatch_Fail_WhenMaxEventsIsZero()
	{
		var options = new CloudEventBatchOptions { MaxEvents = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(CloudEventBatchOptions.MaxEvents)));
	}

	[Fact]
	public void CloudEventBatch_Fail_WhenMaxBatchSizeBytesIsZero()
	{
		var options = new CloudEventBatchOptions { MaxBatchSizeBytes = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(CloudEventBatchOptions.MaxBatchSizeBytes)));
	}

	#endregion

	#region DeadLetterOptions

	[Fact]
	public void DeadLetter_Succeed_WithDefaults()
	{
		var options = new DeadLetterOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void DeadLetter_Fail_WhenMaxAttemptsIsZero()
	{
		var options = new DeadLetterOptions { MaxAttempts = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(DeadLetterOptions.MaxAttempts)));
	}

	#endregion

	#region KeyDerivationOptions

	[Fact]
	public void KeyDerivation_Succeed_WithDefaults()
	{
		var options = new KeyDerivationOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void KeyDerivation_Fail_WhenIterationsIsZero()
	{
		var options = new KeyDerivationOptions { Iterations = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(KeyDerivationOptions.Iterations)));
	}

	#endregion

	#region PipelineOptions

	[Fact]
	public void Pipeline_Succeed_WithDefaults()
	{
		var options = new PipelineOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void Pipeline_Fail_WhenMaxConcurrencyIsZero()
	{
		var options = new PipelineOptions { MaxConcurrency = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(PipelineOptions.MaxConcurrency)));
	}

	#endregion

	#region TracingOptions

	[Fact]
	public void Tracing_Succeed_WithDefaults()
	{
		var options = new TracingOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void Tracing_Fail_WhenSamplingRatioExceedsMax()
	{
		var options = new TracingOptions { SamplingRatio = 1.1 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(TracingOptions.SamplingRatio)));
	}

	[Fact]
	public void Tracing_Fail_WhenSamplingRatioIsNegative()
	{
		var options = new TracingOptions { SamplingRatio = -0.1 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(TracingOptions.SamplingRatio)));
	}

	#endregion

	#region SerializationOptions

	[Fact]
	public void Serialization_Succeed_WithDefaults()
	{
		var options = new SerializationOptions();
		TryValidate(options, out var results).ShouldBeTrue();
		results.ShouldBeEmpty();
	}

	[Fact]
	public void Serialization_Fail_WhenDefaultBufferSizeIsZero()
	{
		var options = new SerializationOptions { DefaultBufferSize = 0 };
		TryValidate(options, out var results).ShouldBeFalse();
		results.ShouldContain(r => r.MemberNames.Contains(nameof(SerializationOptions.DefaultBufferSize)));
	}

	#endregion
}
