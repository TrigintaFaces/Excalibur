// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Common;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class CommonOptionsShould
{
	[Fact]
	public void FlowControlOptionsHaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new FlowControlOptions();

		// Assert
		options.MaxOutstandingMessages.ShouldBe(1000);
		options.MaxOutstandingBytes.ShouldBe(100_000_000);
		options.LimitExceededBehavior.ShouldBeTrue();
	}

	[Fact]
	public void FlowControlOptionsAllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new FlowControlOptions
		{
			MaxOutstandingMessages = 500,
			MaxOutstandingBytes = 50_000_000,
			LimitExceededBehavior = false,
		};

		// Assert
		options.MaxOutstandingMessages.ShouldBe(500);
		options.MaxOutstandingBytes.ShouldBe(50_000_000);
		options.LimitExceededBehavior.ShouldBeFalse();
	}

	[Fact]
	public void PubSubRetryOptionsHaveCorrectDefaults()
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
	public void PubSubRetryOptionsAllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new PubSubRetryOptions
		{
			InitialRetryDelay = TimeSpan.FromMilliseconds(200),
			RetryDelayMultiplier = 3.0,
			MaxRetryDelay = TimeSpan.FromSeconds(120),
			TotalTimeout = TimeSpan.FromMinutes(20),
		};

		// Assert
		options.InitialRetryDelay.ShouldBe(TimeSpan.FromMilliseconds(200));
		options.RetryDelayMultiplier.ShouldBe(3.0);
		options.MaxRetryDelay.ShouldBe(TimeSpan.FromSeconds(120));
		options.TotalTimeout.ShouldBe(TimeSpan.FromMinutes(20));
	}
}
