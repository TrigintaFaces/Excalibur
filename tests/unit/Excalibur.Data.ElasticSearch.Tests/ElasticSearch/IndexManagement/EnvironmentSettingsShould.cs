// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="EnvironmentOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify environment-specific settings defaults.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class EnvironmentOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Name_DefaultsToProduction()
	{
		// Arrange & Act
		var settings = new EnvironmentOptions();

		// Assert
		settings.Name.ShouldBe("Production");
	}

	[Fact]
	public void UseDevelopmentSettings_DefaultsToFalse()
	{
		// Arrange & Act
		var settings = new EnvironmentOptions();

		// Assert
		settings.UseDevelopmentSettings.ShouldBeFalse();
	}

	[Fact]
	public void ReplicaCount_DefaultsToOne()
	{
		// Arrange & Act
		var settings = new EnvironmentOptions();

		// Assert
		settings.ReplicaCount.ShouldBe(1);
	}

	[Fact]
	public void RefreshInterval_DefaultsToOneSecond()
	{
		// Arrange & Act
		var settings = new EnvironmentOptions();

		// Assert
		settings.RefreshInterval.ShouldBe(TimeSpan.FromSeconds(1));
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange & Act
		var settings = new EnvironmentOptions
		{
			Name = "Development",
			UseDevelopmentSettings = true,
			ReplicaCount = 0,
			RefreshInterval = TimeSpan.FromSeconds(5)
		};

		// Assert
		settings.Name.ShouldBe("Development");
		settings.UseDevelopmentSettings.ShouldBeTrue();
		settings.ReplicaCount.ShouldBe(0);
		settings.RefreshInterval.ShouldBe(TimeSpan.FromSeconds(5));
	}

	#endregion

	#region Environment Name Tests

	[Theory]
	[InlineData("Development")]
	[InlineData("Staging")]
	[InlineData("Production")]
	[InlineData("Testing")]
	public void Name_AcceptsStandardEnvironments(string environment)
	{
		// Arrange & Act
		var settings = new EnvironmentOptions
		{
			Name = environment
		};

		// Assert
		settings.Name.ShouldBe(environment);
	}

	#endregion

	#region Replica Count Tests

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public void ReplicaCount_AcceptsVariousValues(int replicas)
	{
		// Arrange & Act
		var settings = new EnvironmentOptions
		{
			ReplicaCount = replicas
		};

		// Assert
		settings.ReplicaCount.ShouldBe(replicas);
	}

	#endregion

	#region Refresh Interval Tests

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(30)]
	public void RefreshInterval_AcceptsVariousSeconds(int seconds)
	{
		// Arrange & Act
		var settings = new EnvironmentOptions
		{
			RefreshInterval = TimeSpan.FromSeconds(seconds)
		};

		// Assert
		settings.RefreshInterval.ShouldBe(TimeSpan.FromSeconds(seconds));
	}

	#endregion

	#region Development Settings Tests

	[Fact]
	public void DevelopmentEnvironment_TypicalSettings()
	{
		// Arrange & Act - Typical development settings
		var settings = new EnvironmentOptions
		{
			Name = "Development",
			UseDevelopmentSettings = true,
			ReplicaCount = 0,
			RefreshInterval = TimeSpan.FromSeconds(1)
		};

		// Assert
		settings.Name.ShouldBe("Development");
		settings.UseDevelopmentSettings.ShouldBeTrue();
		settings.ReplicaCount.ShouldBe(0); // No replicas in dev
	}

	[Fact]
	public void ProductionEnvironment_TypicalSettings()
	{
		// Arrange & Act - Typical production settings
		var settings = new EnvironmentOptions
		{
			Name = "Production",
			UseDevelopmentSettings = false,
			ReplicaCount = 2,
			RefreshInterval = TimeSpan.FromSeconds(30)
		};

		// Assert
		settings.Name.ShouldBe("Production");
		settings.UseDevelopmentSettings.ShouldBeFalse();
		settings.ReplicaCount.ShouldBe(2); // Multiple replicas for HA
		settings.RefreshInterval.ShouldBe(TimeSpan.FromSeconds(30)); // Longer for performance
	}

	#endregion
}
