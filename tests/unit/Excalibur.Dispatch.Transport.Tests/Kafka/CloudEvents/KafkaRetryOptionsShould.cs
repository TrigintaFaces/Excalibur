// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaRetryOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new KafkaRetryOptions();

		// Assert
		options.MaxRetries.ShouldBe(3);
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
			MaxRetries = 5,
			RetryDelay = TimeSpan.FromMilliseconds(200),
			UseExponentialBackoff = false,
			BackoffMultiplier = 3.0,
			MaxRetryDelay = TimeSpan.FromMinutes(1),
			UseJitter = false,
		};

		// Assert
		options.MaxRetries.ShouldBe(5);
		options.RetryDelay.ShouldBe(TimeSpan.FromMilliseconds(200));
		options.UseExponentialBackoff.ShouldBeFalse();
		options.BackoffMultiplier.ShouldBe(3.0);
		options.MaxRetryDelay.ShouldBe(TimeSpan.FromMinutes(1));
		options.UseJitter.ShouldBeFalse();
	}
}
