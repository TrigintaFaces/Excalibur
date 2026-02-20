// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.Common;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AzureRetryOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new AzureRetryOptions();

		// Assert
		options.MaxRetries.ShouldBe(3);
		options.Delay.ShouldBe(TimeSpan.FromSeconds(1));
		options.MaxDelay.ShouldBe(TimeSpan.FromSeconds(10));
		options.Mode.ShouldBe(RetryMode.Exponential);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new AzureRetryOptions
		{
			MaxRetries = 5,
			Delay = TimeSpan.FromSeconds(2),
			MaxDelay = TimeSpan.FromSeconds(30),
			Mode = RetryMode.Fixed,
		};

		// Assert
		options.MaxRetries.ShouldBe(5);
		options.Delay.ShouldBe(TimeSpan.FromSeconds(2));
		options.MaxDelay.ShouldBe(TimeSpan.FromSeconds(30));
		options.Mode.ShouldBe(RetryMode.Fixed);
	}

	[Fact]
	public void RetryModeEnumHaveCorrectValues()
	{
		// Assert
		((int)RetryMode.Fixed).ShouldBe(0);
		((int)RetryMode.Exponential).ShouldBe(1);
	}
}
