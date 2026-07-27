// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.CloudEvents;

/// <summary>
/// 6nyyj6 (S868) — independent (author≠impl, TestsDeveloper) lock for
/// <see cref="AwsEventBridgeCloudEventOptionsValidator"/>. Non-vacuous: RED on the pre-wire no-op, GREEN on
/// the shipped rules (EventBusName + SourcePrefix required, PutEvents batch limit 1–10, replay requires an
/// archive name).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class AwsEventBridgeCloudEventOptionsValidatorShould
{
	private readonly AwsEventBridgeCloudEventOptionsValidator _validator = new();

	[Fact]
	public void SucceedForDefaultOptions() =>
		_validator.Validate(null, new AwsEventBridgeCloudEventOptions()).Succeeded.ShouldBeTrue();

	[Fact]
	public void FailWhenOptionsIsNull() =>
		_validator.Validate(null, null!).Failed.ShouldBeTrue();

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void FailWhenEventBusNameIsBlank(string? value)
	{
		var result = _validator.Validate(null, new AwsEventBridgeCloudEventOptions { EventBusName = value! });

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(AwsEventBridgeCloudEventOptions.EventBusName));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void FailWhenSourcePrefixIsBlank(string? value)
	{
		var result = _validator.Validate(null, new AwsEventBridgeCloudEventOptions { SourcePrefix = value! });

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(AwsEventBridgeCloudEventOptions.SourcePrefix));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(11)]
	public void FailWhenMaxBatchSizeOutOfRange(int value)
	{
		var result = _validator.Validate(null, new AwsEventBridgeCloudEventOptions { MaxBatchSize = value });

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(AwsEventBridgeCloudEventOptions.MaxBatchSize));
	}

	[Fact]
	public void FailWhenReplayEnabledWithoutArchiveName()
	{
		var result = _validator.Validate(null, new AwsEventBridgeCloudEventOptions
		{
			EnableReplay = true,
			ReplayArchiveName = null,
		});

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(AwsEventBridgeCloudEventOptions.ReplayArchiveName));
	}

	[Fact]
	public void SucceedWhenReplayEnabledWithArchiveName()
	{
		var result = _validator.Validate(null, new AwsEventBridgeCloudEventOptions
		{
			EnableReplay = true,
			ReplayArchiveName = "archive-1",
		});

		result.Succeeded.ShouldBeTrue();
	}
}
