// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.DeadLetter;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class DeadLetterAnalyticsOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new DeadLetterAnalyticsOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.DeadLetterSubscription.ShouldBeNull();
		options.CollectionInterval.ShouldBe(TimeSpan.FromMinutes(1));
		options.ReportingInterval.ShouldBe(TimeSpan.FromMinutes(5));
		options.BatchSize.ShouldBe(10);
		options.EnableDetailedLogging.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new DeadLetterAnalyticsOptions
		{
			Enabled = false,
			CollectionInterval = TimeSpan.FromMinutes(5),
			ReportingInterval = TimeSpan.FromMinutes(15),
			BatchSize = 50,
			EnableDetailedLogging = true,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.CollectionInterval.ShouldBe(TimeSpan.FromMinutes(5));
		options.ReportingInterval.ShouldBe(TimeSpan.FromMinutes(15));
		options.BatchSize.ShouldBe(50);
		options.EnableDetailedLogging.ShouldBeTrue();
	}
}
