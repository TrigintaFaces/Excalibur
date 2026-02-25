// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Monitoring;

namespace Excalibur.Data.Tests.ElasticSearch.Monitoring;

/// <summary>
/// Unit tests for the <see cref="ElasticsearchClusterHealth"/> class.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.2): Elasticsearch Phase 2 unit tests.
/// Tests verify cluster health data model properties and defaults.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Monitoring")]
public sealed class ElasticsearchClusterHealthShould
{
	#region Default Value Tests

	[Fact]
	public void DefaultIsHealthy_ToFalse()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth();

		// Assert
		health.IsHealthy.ShouldBeFalse();
	}

	[Fact]
	public void DefaultClusterName_ToNull()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth();

		// Assert
		health.ClusterName.ShouldBeNull();
	}

	[Fact]
	public void DefaultNumberOfNodes_ToNull()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth();

		// Assert
		health.NumberOfNodes.ShouldBeNull();
	}

	[Fact]
	public void DefaultErrorMessage_ToNull()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth();

		// Assert
		health.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void DefaultTimestamp_ToDefaultDateTime()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth();

		// Assert
		health.Timestamp.ShouldBe(default);
	}

	#endregion

	#region Property Assignment Tests

	[Fact]
	public void AllowIsHealthy_ToBeSetToTrue()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth { IsHealthy = true };

		// Assert
		health.IsHealthy.ShouldBeTrue();
	}

	[Theory]
	[InlineData(HealthStatus.Green)]
	[InlineData(HealthStatus.Yellow)]
	[InlineData(HealthStatus.Red)]
	public void AllowStatus_ToBeSet(HealthStatus status)
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth { Status = status };

		// Assert
		health.Status.ShouldBe(status);
	}

	[Fact]
	public void AllowClusterName_ToBeSet()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth { ClusterName = "test-cluster" };

		// Assert
		health.ClusterName.ShouldBe("test-cluster");
	}

	[Fact]
	public void AllowNumberOfNodes_ToBeSet()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth { NumberOfNodes = 3 };

		// Assert
		health.NumberOfNodes.ShouldBe(3);
	}

	[Fact]
	public void AllowNumberOfDataNodes_ToBeSet()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth { NumberOfDataNodes = 2 };

		// Assert
		health.NumberOfDataNodes.ShouldBe(2);
	}

	[Fact]
	public void AllowActivePrimaryShards_ToBeSet()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth { ActivePrimaryShards = 10 };

		// Assert
		health.ActivePrimaryShards.ShouldBe(10);
	}

	[Fact]
	public void AllowActiveShards_ToBeSet()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth { ActiveShards = 20 };

		// Assert
		health.ActiveShards.ShouldBe(20);
	}

	[Fact]
	public void AllowRelocatingShards_ToBeSet()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth { RelocatingShards = 1 };

		// Assert
		health.RelocatingShards.ShouldBe(1);
	}

	[Fact]
	public void AllowInitializingShards_ToBeSet()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth { InitializingShards = 2 };

		// Assert
		health.InitializingShards.ShouldBe(2);
	}

	[Fact]
	public void AllowUnassignedShards_ToBeSet()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth { UnassignedShards = 0 };

		// Assert
		health.UnassignedShards.ShouldBe(0);
	}

	[Fact]
	public void AllowDelayedUnassignedShards_ToBeSet()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth { DelayedUnassignedShards = 0 };

		// Assert
		health.DelayedUnassignedShards.ShouldBe(0);
	}

	[Fact]
	public void AllowNumberOfPendingTasks_ToBeSet()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth { NumberOfPendingTasks = 5 };

		// Assert
		health.NumberOfPendingTasks.ShouldBe(5);
	}

	[Fact]
	public void AllowNumberOfInFlightFetch_ToBeSet()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth { NumberOfInFlightFetch = 3 };

		// Assert
		health.NumberOfInFlightFetch.ShouldBe(3);
	}

	[Fact]
	public void AllowTaskMaxWaitingInQueueMillis_ToBeSet()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth { TaskMaxWaitingInQueueMillis = 1000 };

		// Assert
		health.TaskMaxWaitingInQueueMillis.ShouldBe(1000);
	}

	[Fact]
	public void AllowActiveShardsPercentAsNumber_ToBeSet()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth { ActiveShardsPercentAsNumber = 100.0 };

		// Assert
		health.ActiveShardsPercentAsNumber.ShouldBe(100.0);
	}

	[Fact]
	public void AllowTotalDocuments_ToBeSet()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth { TotalDocuments = 1000000 };

		// Assert
		health.TotalDocuments.ShouldBe(1000000);
	}

	[Fact]
	public void AllowTotalSizeInBytes_ToBeSet()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth { TotalSizeInBytes = 1073741824 }; // 1GB

		// Assert
		health.TotalSizeInBytes.ShouldBe(1073741824);
	}

	[Fact]
	public void AllowNodeCount_ToBeSet()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth { NodeCount = 5 };

		// Assert
		health.NodeCount.ShouldBe(5);
	}

	[Fact]
	public void AllowErrorMessage_ToBeSet()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth { ErrorMessage = "Connection timeout" };

		// Assert
		health.ErrorMessage.ShouldBe("Connection timeout");
	}

	[Fact]
	public void AllowTimestamp_ToBeSet()
	{
		// Arrange
		var timestamp = DateTime.UtcNow;

		// Act
		var health = new ElasticsearchClusterHealth { Timestamp = timestamp };

		// Assert
		health.Timestamp.ShouldBe(timestamp);
	}

	#endregion

	#region Instance Creation Tests

	[Fact]
	public void CreateNewInstance_WithDefaultConstructor()
	{
		// Act
		var health = new ElasticsearchClusterHealth();

		// Assert
		health.ShouldNotBeNull();
	}

	[Fact]
	public void CreateNewInstance_RepresentingHealthyCluster()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth
		{
			IsHealthy = true,
			Status = HealthStatus.Green,
			ClusterName = "production-cluster",
			NumberOfNodes = 3,
			NumberOfDataNodes = 2,
			ActivePrimaryShards = 50,
			ActiveShards = 100,
			RelocatingShards = 0,
			InitializingShards = 0,
			UnassignedShards = 0,
			ActiveShardsPercentAsNumber = 100.0,
			Timestamp = DateTime.UtcNow
		};

		// Assert
		health.IsHealthy.ShouldBeTrue();
		health.Status.ShouldBe(HealthStatus.Green);
		health.UnassignedShards.ShouldBe(0);
		health.ActiveShardsPercentAsNumber.ShouldBe(100.0);
	}

	[Fact]
	public void CreateNewInstance_RepresentingUnhealthyCluster()
	{
		// Arrange & Act
		var health = new ElasticsearchClusterHealth
		{
			IsHealthy = false,
			Status = HealthStatus.Red,
			ClusterName = "degraded-cluster",
			UnassignedShards = 10,
			ErrorMessage = "Some shards are unassigned",
			Timestamp = DateTime.UtcNow
		};

		// Assert
		health.IsHealthy.ShouldBeFalse();
		health.Status.ShouldBe(HealthStatus.Red);
		health.UnassignedShards.ShouldBe(10);
		health.ErrorMessage.ShouldNotBeNull();
	}

	#endregion
}
