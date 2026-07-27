// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.CloudEvents;

/// <summary>
/// 6nyyj6 (S868) — independent (author≠impl, TestsDeveloper) lock for
/// <see cref="AwsSqsCloudEventOptionsValidator"/>. Non-vacuous: each case is RED on the pre-wire no-op
/// (an unwired validator would never reject) and GREEN on the shipped <c>IValidateOptions&lt;T&gt;</c> rules
/// (SQS batch limit 1–10, DelaySeconds 0–900, non-negative compression threshold).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class AwsSqsCloudEventOptionsValidatorShould
{
	private readonly AwsSqsCloudEventOptionsValidator _validator = new();

	[Fact]
	public void SucceedForDefaultOptions() =>
		_validator.Validate(null, new AwsSqsCloudEventOptions()).Succeeded.ShouldBeTrue();

	[Fact]
	public void FailWhenOptionsIsNull() =>
		_validator.Validate(null, null!).Failed.ShouldBeTrue();

	[Theory]
	[InlineData(0)]
	[InlineData(11)]
	[InlineData(-1)]
	public void FailWhenMaxBatchSizeOutOfRange(int value)
	{
		var result = _validator.Validate(null, new AwsSqsCloudEventOptions { MaxBatchSize = value });

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(AwsSqsCloudEventOptions.MaxBatchSize));
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(901)]
	public void FailWhenDelaySecondsOutOfRange(int value)
	{
		var result = _validator.Validate(null, new AwsSqsCloudEventOptions { DelaySeconds = value });

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(AwsSqsCloudEventOptions.DelaySeconds));
	}

	[Fact]
	public void FailWhenCompressionThresholdIsNegative()
	{
		var result = _validator.Validate(null, new AwsSqsCloudEventOptions { CompressionThreshold = -1 });

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(AwsSqsCloudEventOptions.CompressionThreshold));
	}

	[Fact]
	public void ReportMultipleFailures_WhenMultipleConstraintsViolated()
	{
		var result = _validator.Validate(null, new AwsSqsCloudEventOptions
		{
			MaxBatchSize = 0,
			DelaySeconds = 901,
			CompressionThreshold = -1,
		});

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(AwsSqsCloudEventOptions.MaxBatchSize));
		result.FailureMessage.ShouldContain(nameof(AwsSqsCloudEventOptions.DelaySeconds));
		result.FailureMessage.ShouldContain(nameof(AwsSqsCloudEventOptions.CompressionThreshold));
	}
}
