// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.CloudEvents;

/// <summary>
/// 6nyyj6 (S868) — independent (author≠impl, TestsDeveloper) lock for
/// <see cref="AzureServiceBusCloudEventOptionsValidator"/>. Non-vacuous: RED on the pre-wire no-op, GREEN on
/// the shipped rules (positive message size + delivery count; a positive duplicate-detection window when
/// enabled; positive TTL when set).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class AzureServiceBusCloudEventOptionsValidatorShould
{
	private readonly AzureServiceBusCloudEventOptionsValidator _validator = new();

	[Fact]
	public void SucceedForDefaultOptions() =>
		_validator.Validate(null, new AzureServiceBusCloudEventOptions()).Succeeded.ShouldBeTrue();

	[Fact]
	public void FailWhenOptionsIsNull() =>
		_validator.Validate(null, null!).Failed.ShouldBeTrue();

	[Fact]
	public void FailWhenTimeToLiveIsNonPositive_WhenSet()
	{
		var result = _validator.Validate(null, new AzureServiceBusCloudEventOptions
		{
			TimeToLive = TimeSpan.Zero,
		});

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(AzureServiceBusCloudEventOptions.TimeToLive));
	}

	[Fact]
	public void SucceedWhenTimeToLiveIsNull()
	{
		var result = _validator.Validate(null, new AzureServiceBusCloudEventOptions { TimeToLive = null });

		result.Succeeded.ShouldBeTrue();
	}
}
