// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub;

/// <summary>
/// Verifies cross-property validation in <see cref="StreamingPullOptionsValidator"/>.
/// Sprint 564 S564.54: StreamingPull IValidateOptions tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class StreamingPullOptionsValidatorShould
{
	private readonly StreamingPullOptionsValidator _sut = new();

	[Fact]
	public void Succeed_WithValidDefaults()
	{
		var options = new StreamingPullOptions();
		var result = _sut.Validate(null, options);
		result.Succeeded.ShouldBeTrue();
	}

	#region Range validation

	[Theory]
	[InlineData(0)]
	[InlineData(33)]
	public void Fail_WhenConcurrentStreamsOutOfRange(int value)
	{
		var options = new StreamingPullOptions { ConcurrentStreams = value };
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(StreamingPullOptions.ConcurrentStreams));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(32)]
	public void Succeed_WhenConcurrentStreamsAtBoundary(int value)
	{
		var options = new StreamingPullOptions { ConcurrentStreams = value };
		var result = _sut.Validate(null, options);
		result.Succeeded.ShouldBeTrue();
	}

	[Theory]
	[InlineData(9)]
	[InlineData(10001)]
	public void Fail_WhenMaxOutstandingMessagesOutOfRange(int value)
	{
		var options = new StreamingPullOptions { MaxOutstandingMessagesPerStream = value };
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(StreamingPullOptions.MaxOutstandingMessagesPerStream));
	}

	[Theory]
	[InlineData(1048575L)]   // < 1MB
	[InlineData(1073741825L)] // > 1GB
	public void Fail_WhenMaxOutstandingBytesOutOfRange(long value)
	{
		var options = new StreamingPullOptions { MaxOutstandingBytesPerStream = value };
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(StreamingPullOptions.MaxOutstandingBytesPerStream));
	}

	[Theory]
	[InlineData(9)]
	[InlineData(601)]
	public void Fail_WhenStreamAckDeadlineOutOfRange(int value)
	{
		var options = new StreamingPullOptions { StreamAckDeadlineSeconds = value };
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(StreamingPullOptions.StreamAckDeadlineSeconds));
	}

	[Theory]
	[InlineData(49)]
	[InlineData(96)]
	public void Fail_WhenAckExtensionThresholdOutOfRange(int value)
	{
		var options = new StreamingPullOptions { AckExtensionThresholdPercent = value };
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(StreamingPullOptions.AckExtensionThresholdPercent));
	}

	#endregion

	#region Cross-property validation

	[Fact]
	public void Fail_WhenStreamIdleTimeoutNotGreaterThanAckDeadline()
	{
		var options = new StreamingPullOptions
		{
			StreamAckDeadlineSeconds = 60,
			StreamIdleTimeout = TimeSpan.FromSeconds(60), // Equal, not greater
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(StreamingPullOptions.StreamIdleTimeout));
	}

	[Fact]
	public void Fail_WhenStreamIdleTimeoutLessThanAckDeadline()
	{
		var options = new StreamingPullOptions
		{
			StreamAckDeadlineSeconds = 60,
			StreamIdleTimeout = TimeSpan.FromSeconds(30),
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(StreamingPullOptions.StreamIdleTimeout));
	}

	[Fact]
	public void Fail_WhenHealthCheckIntervalExceedsStreamIdleTimeout()
	{
		var options = new StreamingPullOptions
		{
			StreamIdleTimeout = TimeSpan.FromSeconds(90),
			HealthCheckInterval = TimeSpan.FromSeconds(90), // Equal, not less
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(StreamingPullOptions.HealthCheckInterval));
	}

	[Fact]
	public void Succeed_WhenCrossPropertyConstraintsMet()
	{
		var options = new StreamingPullOptions
		{
			StreamAckDeadlineSeconds = 30,
			StreamIdleTimeout = TimeSpan.FromSeconds(60),
			HealthCheckInterval = TimeSpan.FromSeconds(30),
		};
		var result = _sut.Validate(null, options);
		result.Succeeded.ShouldBeTrue();
	}

	#endregion

	[Fact]
	public void CollectMultipleFailures()
	{
		var options = new StreamingPullOptions
		{
			ConcurrentStreams = 0,
			MaxOutstandingMessagesPerStream = 0,
			StreamAckDeadlineSeconds = 5,
			AckExtensionThresholdPercent = 10,
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(StreamingPullOptions.ConcurrentStreams));
		result.FailureMessage.ShouldContain(nameof(StreamingPullOptions.MaxOutstandingMessagesPerStream));
		result.FailureMessage.ShouldContain(nameof(StreamingPullOptions.StreamAckDeadlineSeconds));
		result.FailureMessage.ShouldContain(nameof(StreamingPullOptions.AckExtensionThresholdPercent));
	}
}
