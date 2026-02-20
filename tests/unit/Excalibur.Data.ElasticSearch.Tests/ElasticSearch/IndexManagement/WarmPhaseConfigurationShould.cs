// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="WarmPhaseConfiguration"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify warm phase-specific properties.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class WarmPhaseConfigurationShould
{
	#region Default Value Tests

	[Fact]
	public void NumberOfReplicas_DefaultsToNull()
	{
		// Arrange & Act
		var config = new WarmPhaseConfiguration();

		// Assert
		config.NumberOfReplicas.ShouldBeNull();
	}

	[Fact]
	public void ShrinkNumberOfShards_DefaultsToNull()
	{
		// Arrange & Act
		var config = new WarmPhaseConfiguration();

		// Assert
		config.ShrinkNumberOfShards.ShouldBeNull();
	}

	[Fact]
	public void Priority_DefaultsToNull()
	{
		// Arrange & Act
		var config = new WarmPhaseConfiguration();

		// Assert
		config.Priority.ShouldBeNull();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange & Act
		var config = new WarmPhaseConfiguration
		{
			MinAge = TimeSpan.FromDays(7),
			NumberOfReplicas = 1,
			ShrinkNumberOfShards = 1,
			Priority = 50
		};

		// Assert
		config.MinAge.ShouldBe(TimeSpan.FromDays(7));
		config.NumberOfReplicas.ShouldBe(1);
		config.ShrinkNumberOfShards.ShouldBe(1);
		config.Priority.ShouldBe(50);
	}

	#endregion

	#region Replica Count Tests

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public void NumberOfReplicas_AcceptsVariousValues(int replicas)
	{
		// Arrange & Act
		var config = new WarmPhaseConfiguration
		{
			NumberOfReplicas = replicas
		};

		// Assert
		config.NumberOfReplicas.ShouldBe(replicas);
	}

	#endregion

	#region Shrink Configuration Tests

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(5)]
	public void ShrinkNumberOfShards_AcceptsVariousValues(int shards)
	{
		// Arrange & Act
		var config = new WarmPhaseConfiguration
		{
			ShrinkNumberOfShards = shards
		};

		// Assert
		config.ShrinkNumberOfShards.ShouldBe(shards);
	}

	#endregion

	#region Priority Tests

	[Theory]
	[InlineData(1)]
	[InlineData(25)]
	[InlineData(50)]
	public void Priority_AcceptsVariousValues(int priority)
	{
		// Arrange & Act
		var config = new WarmPhaseConfiguration
		{
			Priority = priority
		};

		// Assert
		config.Priority.ShouldBe(priority);
	}

	#endregion
}
