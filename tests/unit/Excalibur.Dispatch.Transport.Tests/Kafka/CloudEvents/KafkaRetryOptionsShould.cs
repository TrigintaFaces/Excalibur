// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.CloudEvents;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class KafkaRetryOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new KafkaRetryOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(3);
		options.RetryDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
		options.UseExponentialBackoff.ShouldBeTrue();
		options.BackoffMultiplier.ShouldBe(2.0);
		options.MaxRetryDelay.ShouldBe(TimeSpan.FromSeconds(30));
		options.UseJitter.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new KafkaRetryOptions
		{
			MaxRetryAttempts = 5,
			RetryDelay = TimeSpan.FromMilliseconds(200),
			UseExponentialBackoff = false,
			BackoffMultiplier = 3.0,
			MaxRetryDelay = TimeSpan.FromMinutes(1),
			UseJitter = false,
		};

		// Assert
		options.MaxRetryAttempts.ShouldBe(5);
		options.RetryDelay.ShouldBe(TimeSpan.FromMilliseconds(200));
		options.UseExponentialBackoff.ShouldBeFalse();
		options.BackoffMultiplier.ShouldBe(3.0);
		options.MaxRetryDelay.ShouldBe(TimeSpan.FromMinutes(1));
		options.UseJitter.ShouldBeFalse();
	}
}
