// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="LifecycleManagementOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify default values and property initialization.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class LifecycleManagementOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Enabled_DefaultsToTrue()
	{
		// Arrange & Act
		var settings = new LifecycleManagementOptions();

		// Assert
		settings.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void HotPhaseDuration_DefaultsToSevenDays()
	{
		// Arrange & Act
		var settings = new LifecycleManagementOptions();

		// Assert
		settings.HotPhaseDuration.ShouldBe(TimeSpan.FromDays(7));
	}

	[Fact]
	public void WarmPhaseDuration_DefaultsToThirtyDays()
	{
		// Arrange & Act
		var settings = new LifecycleManagementOptions();

		// Assert
		settings.WarmPhaseDuration.ShouldBe(TimeSpan.FromDays(30));
	}

	[Fact]
	public void ColdPhaseDuration_DefaultsToNinetyDays()
	{
		// Arrange & Act
		var settings = new LifecycleManagementOptions();

		// Assert
		settings.ColdPhaseDuration.ShouldBe(TimeSpan.FromDays(90));
	}

	[Fact]
	public void DeleteAfterColdPhase_DefaultsToFalse()
	{
		// Arrange & Act
		var settings = new LifecycleManagementOptions();

		// Assert
		settings.DeleteAfterColdPhase.ShouldBeFalse();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange & Act
		var settings = new LifecycleManagementOptions
		{
			Enabled = false,
			HotPhaseDuration = TimeSpan.FromDays(14),
			WarmPhaseDuration = TimeSpan.FromDays(60),
			ColdPhaseDuration = TimeSpan.FromDays(180),
			DeleteAfterColdPhase = true
		};

		// Assert
		settings.Enabled.ShouldBeFalse();
		settings.HotPhaseDuration.ShouldBe(TimeSpan.FromDays(14));
		settings.WarmPhaseDuration.ShouldBe(TimeSpan.FromDays(60));
		settings.ColdPhaseDuration.ShouldBe(TimeSpan.FromDays(180));
		settings.DeleteAfterColdPhase.ShouldBeTrue();
	}

	#endregion

	#region Phase Duration Order Tests

	[Fact]
	public void DefaultPhaseDurations_AreInAscendingOrder()
	{
		// Arrange
		var settings = new LifecycleManagementOptions();

		// Assert - Hot < Warm < Cold
		settings.HotPhaseDuration.ShouldBeLessThan(settings.WarmPhaseDuration);
		settings.WarmPhaseDuration.ShouldBeLessThan(settings.ColdPhaseDuration);
	}

	[Fact]
	public void TotalLifecycleDuration_IsCalculable()
	{
		// Arrange
		var settings = new LifecycleManagementOptions();

		// Act
		var totalDuration = settings.HotPhaseDuration +
							settings.WarmPhaseDuration +
							settings.ColdPhaseDuration;

		// Assert - 7 + 30 + 90 = 127 days
		totalDuration.ShouldBe(TimeSpan.FromDays(127));
	}

	#endregion

	#region Class Tests

	[Fact]
	public void Class_IsSealed()
	{
		// Assert
		typeof(LifecycleManagementOptions).IsSealed.ShouldBeTrue();
	}

	#endregion
}
