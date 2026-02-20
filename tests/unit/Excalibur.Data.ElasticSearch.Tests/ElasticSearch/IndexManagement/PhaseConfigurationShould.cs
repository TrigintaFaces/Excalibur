// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="PhaseConfiguration"/> abstract class via concrete implementations.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify base class properties through derived classes.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class PhaseConfigurationShould
{
	#region Base Class Property Tests

	[Fact]
	public void MinAge_DefaultsToNull_InHotPhase()
	{
		// Arrange & Act
		var config = new HotPhaseConfiguration();

		// Assert
		config.MinAge.ShouldBeNull();
	}

	[Fact]
	public void MinAge_DefaultsToNull_InWarmPhase()
	{
		// Arrange & Act
		var config = new WarmPhaseConfiguration();

		// Assert
		config.MinAge.ShouldBeNull();
	}

	[Fact]
	public void MinAge_DefaultsToNull_InColdPhase()
	{
		// Arrange & Act
		var config = new ColdPhaseConfiguration();

		// Assert
		config.MinAge.ShouldBeNull();
	}

	[Fact]
	public void MinAge_DefaultsToNull_InDeletePhase()
	{
		// Arrange & Act
		var config = new DeletePhaseConfiguration();

		// Assert
		config.MinAge.ShouldBeNull();
	}

	#endregion

	#region MinAge Initialization Tests

	[Theory]
	[InlineData(1)]
	[InlineData(7)]
	[InlineData(30)]
	public void MinAge_CanBeSetInDays(int days)
	{
		// Arrange & Act
		var config = new WarmPhaseConfiguration
		{
			MinAge = TimeSpan.FromDays(days)
		};

		// Assert
		config.MinAge.ShouldBe(TimeSpan.FromDays(days));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(12)]
	[InlineData(24)]
	public void MinAge_CanBeSetInHours(int hours)
	{
		// Arrange & Act
		var config = new HotPhaseConfiguration
		{
			MinAge = TimeSpan.FromHours(hours)
		};

		// Assert
		config.MinAge.ShouldBe(TimeSpan.FromHours(hours));
	}

	#endregion

	#region Inheritance Tests

	[Fact]
	public void HotPhase_InheritsFromPhaseConfiguration()
	{
		// Assert
		typeof(HotPhaseConfiguration).BaseType.ShouldBe(typeof(PhaseConfiguration));
	}

	[Fact]
	public void WarmPhase_InheritsFromPhaseConfiguration()
	{
		// Assert
		typeof(WarmPhaseConfiguration).BaseType.ShouldBe(typeof(PhaseConfiguration));
	}

	[Fact]
	public void ColdPhase_InheritsFromPhaseConfiguration()
	{
		// Assert
		typeof(ColdPhaseConfiguration).BaseType.ShouldBe(typeof(PhaseConfiguration));
	}

	[Fact]
	public void DeletePhase_InheritsFromPhaseConfiguration()
	{
		// Assert
		typeof(DeletePhaseConfiguration).BaseType.ShouldBe(typeof(PhaseConfiguration));
	}

	#endregion
}
