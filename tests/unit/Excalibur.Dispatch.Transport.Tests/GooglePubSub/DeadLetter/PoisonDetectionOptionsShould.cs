// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.DeadLetter;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class PoisonDetectionOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new PoisonDetectionOptions();

		// Assert
		options.MaxFailuresBeforePoison.ShouldBe(5);
		options.RapidFailureCount.ShouldBe(3);
		options.RapidFailureWindow.ShouldBe(TimeSpan.FromMinutes(1));
		options.ConsistentExceptionThreshold.ShouldBe(0.8);
		options.TimeoutThreshold.ShouldBe(0.7);
		options.LoopDetectionThreshold.ShouldBe(10);
		options.HistoryRetentionPeriod.ShouldBe(TimeSpan.FromHours(24));
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new PoisonDetectionOptions
		{
			MaxFailuresBeforePoison = 10,
			RapidFailureCount = 5,
			RapidFailureWindow = TimeSpan.FromMinutes(2),
			ConsistentExceptionThreshold = 0.9,
			TimeoutThreshold = 0.5,
			LoopDetectionThreshold = 20,
			HistoryRetentionPeriod = TimeSpan.FromHours(48),
		};

		// Assert
		options.MaxFailuresBeforePoison.ShouldBe(10);
		options.RapidFailureCount.ShouldBe(5);
		options.RapidFailureWindow.ShouldBe(TimeSpan.FromMinutes(2));
		options.ConsistentExceptionThreshold.ShouldBe(0.9);
		options.TimeoutThreshold.ShouldBe(0.5);
		options.LoopDetectionThreshold.ShouldBe(20);
		options.HistoryRetentionPeriod.ShouldBe(TimeSpan.FromHours(48));
	}
}
