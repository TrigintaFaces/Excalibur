// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="HotPhaseConfiguration"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify hot phase-specific properties and rollover conditions.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class HotPhaseConfigurationShould
{
	#region Default Value Tests

	[Fact]
	public void Rollover_DefaultsToNull()
	{
		// Arrange & Act
		var config = new HotPhaseConfiguration();

		// Assert
		config.Rollover.ShouldBeNull();
	}

	[Fact]
	public void Priority_DefaultsToNull()
	{
		// Arrange & Act
		var config = new HotPhaseConfiguration();

		// Assert
		config.Priority.ShouldBeNull();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange
		var rollover = new RolloverConditions
		{
			MaxAge = TimeSpan.FromDays(7),
			MaxDocs = 1000000
		};

		// Act
		var config = new HotPhaseConfiguration
		{
			MinAge = TimeSpan.FromHours(1),
			Rollover = rollover,
			Priority = 100
		};

		// Assert
		config.MinAge.ShouldBe(TimeSpan.FromHours(1));
		config.Rollover.ShouldNotBeNull();
		config.Rollover.MaxAge.ShouldBe(TimeSpan.FromDays(7));
		config.Rollover.MaxDocs.ShouldBe(1000000);
		config.Priority.ShouldBe(100);
	}

	#endregion

	#region Rollover Configuration Tests

	[Fact]
	public void Rollover_CanHaveMaxAge()
	{
		// Arrange & Act
		var config = new HotPhaseConfiguration
		{
			Rollover = new RolloverConditions
			{
				MaxAge = TimeSpan.FromDays(30)
			}
		};

		// Assert
		config.Rollover.MaxAge.ShouldBe(TimeSpan.FromDays(30));
	}

	[Fact]
	public void Rollover_CanHaveMaxDocs()
	{
		// Arrange & Act
		var config = new HotPhaseConfiguration
		{
			Rollover = new RolloverConditions
			{
				MaxDocs = 5000000
			}
		};

		// Assert
		config.Rollover.MaxDocs.ShouldBe(5000000);
	}

	[Fact]
	public void Rollover_CanHaveMaxSize()
	{
		// Arrange & Act
		var config = new HotPhaseConfiguration
		{
			Rollover = new RolloverConditions
			{
				MaxSize = "50gb"
			}
		};

		// Assert
		config.Rollover.MaxSize.ShouldBe("50gb");
	}

	#endregion

	#region Priority Tests

	[Theory]
	[InlineData(1)]
	[InlineData(50)]
	[InlineData(100)]
	public void Priority_AcceptsVariousValues(int priority)
	{
		// Arrange & Act
		var config = new HotPhaseConfiguration
		{
			Priority = priority
		};

		// Assert
		config.Priority.ShouldBe(priority);
	}

	#endregion
}
