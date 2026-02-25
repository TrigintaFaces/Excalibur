// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Common;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class PubSubRetryOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new PubSubRetryOptions();

		// Assert
		options.InitialRetryDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
		options.RetryDelayMultiplier.ShouldBe(2.0);
		options.MaxRetryDelay.ShouldBe(TimeSpan.FromSeconds(60));
		options.TotalTimeout.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new PubSubRetryOptions
		{
			InitialRetryDelay = TimeSpan.FromMilliseconds(500),
			RetryDelayMultiplier = 3.0,
			MaxRetryDelay = TimeSpan.FromMinutes(2),
			TotalTimeout = TimeSpan.FromMinutes(30),
		};

		// Assert
		options.InitialRetryDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
		options.RetryDelayMultiplier.ShouldBe(3.0);
		options.MaxRetryDelay.ShouldBe(TimeSpan.FromMinutes(2));
		options.TotalTimeout.ShouldBe(TimeSpan.FromMinutes(30));
	}
}
