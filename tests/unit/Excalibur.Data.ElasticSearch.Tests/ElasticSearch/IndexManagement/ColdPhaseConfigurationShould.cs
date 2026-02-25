// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="ColdPhaseConfiguration"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify cold phase-specific properties.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class ColdPhaseConfigurationShould
{
	#region Default Value Tests

	[Fact]
	public void NumberOfReplicas_DefaultsToNull()
	{
		// Arrange & Act
		var config = new ColdPhaseConfiguration();

		// Assert
		config.NumberOfReplicas.ShouldBeNull();
	}

	[Fact]
	public void Priority_DefaultsToNull()
	{
		// Arrange & Act
		var config = new ColdPhaseConfiguration();

		// Assert
		config.Priority.ShouldBeNull();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange & Act
		var config = new ColdPhaseConfiguration
		{
			MinAge = TimeSpan.FromDays(30),
			NumberOfReplicas = 0,
			Priority = 10
		};

		// Assert
		config.MinAge.ShouldBe(TimeSpan.FromDays(30));
		config.NumberOfReplicas.ShouldBe(0);
		config.Priority.ShouldBe(10);
	}

	#endregion

	#region Replica Count Tests

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	public void NumberOfReplicas_AcceptsVariousValues(int replicas)
	{
		// Arrange & Act
		var config = new ColdPhaseConfiguration
		{
			NumberOfReplicas = replicas
		};

		// Assert
		config.NumberOfReplicas.ShouldBe(replicas);
	}

	#endregion

	#region Priority Tests

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(10)]
	public void Priority_AcceptsVariousValues(int priority)
	{
		// Arrange & Act
		var config = new ColdPhaseConfiguration
		{
			Priority = priority
		};

		// Assert
		config.Priority.ShouldBe(priority);
	}

	#endregion

	#region Long-Term Storage Configuration Tests

	[Fact]
	public void ColdPhase_TypicallyHasLongerMinAge()
	{
		// Arrange & Act
		var config = new ColdPhaseConfiguration
		{
			MinAge = TimeSpan.FromDays(90)
		};

		// Assert
		config.MinAge.ShouldBe(TimeSpan.FromDays(90));
	}

	[Fact]
	public void ColdPhase_TypicallyHasZeroReplicas()
	{
		// Arrange & Act - Cold storage often reduces replicas for cost savings
		var config = new ColdPhaseConfiguration
		{
			NumberOfReplicas = 0
		};

		// Assert
		config.NumberOfReplicas.ShouldBe(0);
	}

	#endregion
}
