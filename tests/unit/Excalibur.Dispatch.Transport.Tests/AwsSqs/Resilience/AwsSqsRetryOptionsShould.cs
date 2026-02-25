// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.AwsSqs;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Resilience;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsSqsRetryOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new AwsSqsRetryOptions();

		// Assert
		options.MaxRetries.ShouldBe(3);
		options.BaseDelay.ShouldBe(TimeSpan.FromMilliseconds(200));
		options.MaxDelay.ShouldBe(TimeSpan.FromSeconds(30));
		options.RetryStrategy.ShouldBe(RetryStrategy.Exponential);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new AwsSqsRetryOptions
		{
			MaxRetries = 5,
			BaseDelay = TimeSpan.FromMilliseconds(500),
			MaxDelay = TimeSpan.FromMinutes(1),
			RetryStrategy = RetryStrategy.Linear,
		};

		// Assert
		options.MaxRetries.ShouldBe(5);
		options.BaseDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
		options.MaxDelay.ShouldBe(TimeSpan.FromMinutes(1));
		options.RetryStrategy.ShouldBe(RetryStrategy.Linear);
	}
}
