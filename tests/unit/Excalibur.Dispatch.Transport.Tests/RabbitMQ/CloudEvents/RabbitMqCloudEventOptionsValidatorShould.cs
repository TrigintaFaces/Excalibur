// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RabbitMqCloudEventOptionsValidatorShould
{
	private readonly RabbitMqCloudEventOptionsValidator _validator = new();

	[Fact]
	public void SucceedForDefaultOptions()
	{
		var options = new RabbitMqCloudEventOptions();

		var result = _validator.Validate(null, options);

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => _validator.Validate(null, null!));
	}

	[Fact]
	public void FailWhenPrefetchCountIsZero()
	{
		var options = new RabbitMqCloudEventOptions { PrefetchCount = 0 };

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RabbitMqCloudEventOptions.PrefetchCount));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void FailWhenMaxMessageSizeBytesIsNotPositive(long value)
	{
		var options = new RabbitMqCloudEventOptions();
		options.Exchange.MaxMessageSizeBytes = value;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RabbitMqCloudEventExchangeOptions.MaxMessageSizeBytes));
	}

	[Fact]
	public void FailWhenMaxRetryAttemptsIsNegative()
	{
		var options = new RabbitMqCloudEventOptions();
		options.DeadLetter.MaxRetryAttempts = -1;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RabbitMqCloudEventDeadLetterOptions.MaxRetryAttempts));
	}

	[Fact]
	public void SucceedWhenMaxRetryAttemptsIsZero()
	{
		var options = new RabbitMqCloudEventOptions();
		options.DeadLetter.MaxRetryAttempts = 0;

		var result = _validator.Validate(null, options);

		result.Succeeded.ShouldBeTrue();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void FailWhenRetryDelayIsNotPositive(int seconds)
	{
		var options = new RabbitMqCloudEventOptions();
		options.DeadLetter.RetryDelay = TimeSpan.FromSeconds(seconds);

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RabbitMqCloudEventDeadLetterOptions.RetryDelay));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void FailWhenNetworkRecoveryIntervalIsNotPositive(int seconds)
	{
		var options = new RabbitMqCloudEventOptions();
		options.Recovery.NetworkRecoveryInterval = TimeSpan.FromSeconds(seconds);

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RabbitMqCloudEventRecoveryOptions.NetworkRecoveryInterval));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void FailWhenMessageTtlIsNotPositive_WhenSet(int seconds)
	{
		var options = new RabbitMqCloudEventOptions();
		options.Exchange.MessageTtl = TimeSpan.FromSeconds(seconds);

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RabbitMqCloudEventExchangeOptions.MessageTtl));
	}

	[Fact]
	public void SucceedWhenMessageTtlIsNull()
	{
		var options = new RabbitMqCloudEventOptions();
		options.Exchange.MessageTtl = null;

		var result = _validator.Validate(null, options);

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ReportMultipleFailures_WhenMultipleConstraintsViolated()
	{
		var options = new RabbitMqCloudEventOptions
		{
			PrefetchCount = 0,
		};
		options.Exchange.MaxMessageSizeBytes = -1;
		options.DeadLetter.MaxRetryAttempts = -1;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RabbitMqCloudEventOptions.PrefetchCount));
		result.FailureMessage.ShouldContain(nameof(RabbitMqCloudEventExchangeOptions.MaxMessageSizeBytes));
		result.FailureMessage.ShouldContain(nameof(RabbitMqCloudEventDeadLetterOptions.MaxRetryAttempts));
	}
}
