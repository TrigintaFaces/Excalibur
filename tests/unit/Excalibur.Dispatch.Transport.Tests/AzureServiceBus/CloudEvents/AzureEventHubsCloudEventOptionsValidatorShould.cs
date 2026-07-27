// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.CloudEvents;

/// <summary>
/// 6nyyj6 (S868) — independent (author≠impl, TestsDeveloper) lock for
/// <see cref="AzureEventHubsCloudEventOptionsValidator"/>. Non-vacuous: RED on the pre-wire no-op, GREEN on
/// the shipped rules (positive batch size + batch bytes; schema-registry namespace required when the schema
/// registry is enabled; capture file-name format required when capture is enabled).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class AzureEventHubsCloudEventOptionsValidatorShould
{
	private readonly AzureEventHubsCloudEventOptionsValidator _validator = new();

	[Fact]
	public void SucceedForDefaultOptions() =>
		_validator.Validate(null, new AzureEventHubsCloudEventOptions()).Succeeded.ShouldBeTrue();

	[Fact]
	public void FailWhenOptionsIsNull() =>
		_validator.Validate(null, null!).Failed.ShouldBeTrue();

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void FailWhenMaxBatchSizeIsNotPositive(int value)
	{
		var result = _validator.Validate(null, new AzureEventHubsCloudEventOptions { MaxBatchSize = value });

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(AzureEventHubsCloudEventOptions.MaxBatchSize));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void FailWhenMaxBatchSizeBytesIsNotPositive(long value)
	{
		var result = _validator.Validate(null, new AzureEventHubsCloudEventOptions { MaxBatchSizeBytes = value });

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(AzureEventHubsCloudEventOptions.MaxBatchSizeBytes));
	}

	[Fact]
	public void FailWhenSchemaRegistryEnabledWithoutNamespace()
	{
		var result = _validator.Validate(null, new AzureEventHubsCloudEventOptions
		{
			UseSchemaRegistry = true,
			SchemaRegistryNamespace = null,
		});

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(AzureEventHubsCloudEventOptions.SchemaRegistryNamespace));
	}

	[Fact]
	public void FailWhenCaptureEnabledWithoutFileNameFormat()
	{
		var result = _validator.Validate(null, new AzureEventHubsCloudEventOptions
		{
			EnableCapture = true,
			CaptureFileNameFormat = "   ",
		});

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(AzureEventHubsCloudEventOptions.CaptureFileNameFormat));
	}

	[Fact]
	public void SucceedWhenSchemaRegistryEnabledWithNamespace()
	{
		var result = _validator.Validate(null, new AzureEventHubsCloudEventOptions
		{
			UseSchemaRegistry = true,
			SchemaRegistryNamespace = "my-namespace",
		});

		result.Succeeded.ShouldBeTrue();
	}
}
