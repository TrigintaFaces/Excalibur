// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="DefaultTemplateOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify default template settings and environment configuration.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class DefaultTemplateOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void DefaultShards_DefaultsToOne()
	{
		// Arrange & Act
		var settings = new DefaultTemplateOptions();

		// Assert
		settings.DefaultShards.ShouldBe(1);
	}

	[Fact]
	public void DefaultReplicas_DefaultsToOne()
	{
		// Arrange & Act
		var settings = new DefaultTemplateOptions();

		// Assert
		settings.DefaultReplicas.ShouldBe(1);
	}

	[Fact]
	public void DefaultRefreshInterval_DefaultsToOneSecond()
	{
		// Arrange & Act
		var settings = new DefaultTemplateOptions();

		// Assert
		settings.DefaultRefreshInterval.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void DefaultPriority_DefaultsTo100()
	{
		// Arrange & Act
		var settings = new DefaultTemplateOptions();

		// Assert
		settings.DefaultPriority.ShouldBe(100);
	}

	[Fact]
	public void Environment_IsNotNull()
	{
		// Arrange & Act
		var settings = new DefaultTemplateOptions();

		// Assert
		settings.Environment.ShouldNotBeNull();
	}

	[Fact]
	public void Environment_HasProductionDefaults()
	{
		// Arrange & Act
		var settings = new DefaultTemplateOptions();

		// Assert
		settings.Environment.Name.ShouldBe("Production");
		settings.Environment.UseDevelopmentSettings.ShouldBeFalse();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange & Act
		var settings = new DefaultTemplateOptions
		{
			DefaultShards = 3,
			DefaultReplicas = 2,
			DefaultRefreshInterval = TimeSpan.FromSeconds(30),
			DefaultPriority = 200,
			Environment = new EnvironmentOptions
			{
				Name = "Staging",
				ReplicaCount = 1
			}
		};

		// Assert
		settings.DefaultShards.ShouldBe(3);
		settings.DefaultReplicas.ShouldBe(2);
		settings.DefaultRefreshInterval.ShouldBe(TimeSpan.FromSeconds(30));
		settings.DefaultPriority.ShouldBe(200);
		settings.Environment.Name.ShouldBe("Staging");
	}

	#endregion

	#region Shard Count Tests

	[Theory]
	[InlineData(1)]
	[InlineData(3)]
	[InlineData(5)]
	public void DefaultShards_AcceptsVariousValues(int shards)
	{
		// Arrange & Act
		var settings = new DefaultTemplateOptions
		{
			DefaultShards = shards
		};

		// Assert
		settings.DefaultShards.ShouldBe(shards);
	}

	#endregion

	#region Replica Count Tests

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public void DefaultReplicas_AcceptsVariousValues(int replicas)
	{
		// Arrange & Act
		var settings = new DefaultTemplateOptions
		{
			DefaultReplicas = replicas
		};

		// Assert
		settings.DefaultReplicas.ShouldBe(replicas);
	}

	#endregion

	#region Priority Tests

	[Theory]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(500)]
	public void DefaultPriority_AcceptsVariousValues(int priority)
	{
		// Arrange & Act
		var settings = new DefaultTemplateOptions
		{
			DefaultPriority = priority
		};

		// Assert
		settings.DefaultPriority.ShouldBe(priority);
	}

	#endregion

	#region Refresh Interval Tests

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(30)]
	public void DefaultRefreshInterval_AcceptsVariousSeconds(int seconds)
	{
		// Arrange & Act
		var settings = new DefaultTemplateOptions
		{
			DefaultRefreshInterval = TimeSpan.FromSeconds(seconds)
		};

		// Assert
		settings.DefaultRefreshInterval.ShouldBe(TimeSpan.FromSeconds(seconds));
	}

	#endregion

	#region Nested Environment Settings Tests

	[Fact]
	public void NestedEnvironment_CanBeReplaced()
	{
		// Arrange
		var customEnvironment = new EnvironmentOptions
		{
			Name = "Development",
			UseDevelopmentSettings = true,
			ReplicaCount = 0
		};

		// Act
		var settings = new DefaultTemplateOptions
		{
			Environment = customEnvironment
		};

		// Assert
		settings.Environment.Name.ShouldBe("Development");
		settings.Environment.UseDevelopmentSettings.ShouldBeTrue();
		settings.Environment.ReplicaCount.ShouldBe(0);
	}

	#endregion
}
