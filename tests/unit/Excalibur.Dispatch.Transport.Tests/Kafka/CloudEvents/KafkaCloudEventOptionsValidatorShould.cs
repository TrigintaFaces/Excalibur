// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.CloudEvents;

/// <summary>
/// 6nyyj6 (S868) — independent (author≠impl, TestsDeveloper) lock for
/// <see cref="KafkaCloudEventOptionsValidator"/>. Non-vacuous: RED on the pre-wire no-op, GREEN on the
/// shipped rules (positive default partition count + replication factor; a non-null producer with a
/// positive max message size).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class KafkaCloudEventOptionsValidatorShould
{
	private readonly KafkaCloudEventOptionsValidator _validator = new();

	[Fact]
	public void SucceedForDefaultOptions() =>
		_validator.Validate(null, new KafkaCloudEventOptions()).Succeeded.ShouldBeTrue();

	[Fact]
	public void FailWhenOptionsIsNull() =>
		_validator.Validate(null, null!).Failed.ShouldBeTrue();

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void FailWhenDefaultPartitionCountIsNotPositive(int value)
	{
		var result = _validator.Validate(null, new KafkaCloudEventOptions { DefaultPartitionCount = value });

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(KafkaCloudEventOptions.DefaultPartitionCount));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void FailWhenDefaultReplicationFactorIsNotPositive(short value)
	{
		var result = _validator.Validate(null, new KafkaCloudEventOptions { DefaultReplicationFactor = value });

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(KafkaCloudEventOptions.DefaultReplicationFactor));
	}

	[Fact]
	public void FailWhenProducerIsNull()
	{
		var result = _validator.Validate(null, new KafkaCloudEventOptions { Producer = null! });

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(KafkaCloudEventOptions.Producer));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void FailWhenProducerMaxMessageSizeIsNotPositive(int value)
	{
		var result = _validator.Validate(null, new KafkaCloudEventOptions
		{
			Producer = new KafkaCloudEventProducerOptions { MaxMessageSizeBytes = value },
		});

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(KafkaCloudEventProducerOptions.MaxMessageSizeBytes));
	}

	[Fact]
	public void ReportMultipleFailures_WhenMultipleConstraintsViolated()
	{
		var result = _validator.Validate(null, new KafkaCloudEventOptions
		{
			DefaultPartitionCount = 0,
			DefaultReplicationFactor = 0,
		});

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(KafkaCloudEventOptions.DefaultPartitionCount));
		result.FailureMessage.ShouldContain(nameof(KafkaCloudEventOptions.DefaultReplicationFactor));
	}
}
