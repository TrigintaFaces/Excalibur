// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Transport;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class TransportOptionsShould
{



	// --- CronTimerOptions ---

	[Fact]
	public void CronTimerOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new CronTimerOptions();

		// Assert
		options.TimeZone.ShouldBe(TimeZoneInfo.Utc);
		options.RunOnStartup.ShouldBeFalse();
		options.PreventOverlap.ShouldBeTrue();
	}

	[Fact]
	public void CronTimerOptions_AllProperties_AreSettable()
	{
		// Arrange
		var pacific = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

		// Act
		var options = new CronTimerOptions
		{
			TimeZone = pacific,
			RunOnStartup = true,
			PreventOverlap = false,
		};

		// Assert
		options.TimeZone.ShouldBe(pacific);
		options.RunOnStartup.ShouldBeTrue();
		options.PreventOverlap.ShouldBeFalse();
	}

}
