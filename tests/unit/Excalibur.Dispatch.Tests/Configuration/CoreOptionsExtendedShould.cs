// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class CoreOptionsExtendedShould
{




	// --- MetricsOptions ---

	[Fact]
	public void MetricsOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new MetricsOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
		options.ExportInterval.ShouldBe(TimeSpan.FromSeconds(30));
		options.CustomTags.ShouldNotBeNull();
		options.CustomTags.ShouldBeEmpty();
	}

	[Fact]
	public void MetricsOptions_CustomTags_CanAddEntries()
	{
		// Arrange
		var options = new MetricsOptions();

		// Act
		options.CustomTags["environment"] = "production";
		options.CustomTags["service"] = "orders";

		// Assert
		options.CustomTags.Count.ShouldBe(2);
		options.CustomTags["environment"].ShouldBe("production");
	}

	[Fact]
	public void MetricsOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new MetricsOptions
		{
			Enabled = true,
			ExportInterval = TimeSpan.FromMinutes(1),
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.ExportInterval.ShouldBe(TimeSpan.FromMinutes(1));
	}
}
