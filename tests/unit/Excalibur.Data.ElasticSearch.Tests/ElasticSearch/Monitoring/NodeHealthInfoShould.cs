// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Monitoring;

namespace Excalibur.Data.Tests.ElasticSearch.Monitoring;

/// <summary>
/// Unit tests for the <see cref="NodeHealthInfo"/> class.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.2): Elasticsearch Phase 2 unit tests.
/// Tests verify node health data model properties and defaults.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Monitoring")]
public sealed class NodeHealthInfoShould
{
	#region Default Value Tests

	[Fact]
	public void DefaultNodeId_ToEmptyString()
	{
		// Arrange & Act
		var node = new NodeHealthInfo();

		// Assert
		node.NodeId.ShouldBe(string.Empty);
	}

	[Fact]
	public void DefaultNodeName_ToNull()
	{
		// Arrange & Act
		var node = new NodeHealthInfo();

		// Assert
		node.NodeName.ShouldBeNull();
	}

	[Fact]
	public void DefaultIsHealthy_ToFalse()
	{
		// Arrange & Act
		var node = new NodeHealthInfo();

		// Assert
		node.IsHealthy.ShouldBeFalse();
	}

	[Fact]
	public void DefaultCpuUsagePercent_ToNull()
	{
		// Arrange & Act
		var node = new NodeHealthInfo();

		// Assert
		node.CpuUsagePercent.ShouldBeNull();
	}

	[Fact]
	public void DefaultMemoryUsagePercent_ToNull()
	{
		// Arrange & Act
		var node = new NodeHealthInfo();

		// Assert
		node.MemoryUsagePercent.ShouldBeNull();
	}

	[Fact]
	public void DefaultDiskUsagePercent_ToNull()
	{
		// Arrange & Act
		var node = new NodeHealthInfo();

		// Assert
		node.DiskUsagePercent.ShouldBeNull();
	}

	[Fact]
	public void DefaultHeapUsagePercent_ToNull()
	{
		// Arrange & Act
		var node = new NodeHealthInfo();

		// Assert
		node.HeapUsagePercent.ShouldBeNull();
	}

	[Fact]
	public void DefaultLoadAverage_ToNull()
	{
		// Arrange & Act
		var node = new NodeHealthInfo();

		// Assert
		node.LoadAverage.ShouldBeNull();
	}

	[Fact]
	public void DefaultErrorMessage_ToNull()
	{
		// Arrange & Act
		var node = new NodeHealthInfo();

		// Assert
		node.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void DefaultLastUpdated_ToDefaultDateTime()
	{
		// Arrange & Act
		var node = new NodeHealthInfo();

		// Assert
		node.LastUpdated.ShouldBe(default);
	}

	#endregion

	#region Property Assignment Tests

	[Fact]
	public void AllowNodeId_ToBeSet()
	{
		// Arrange & Act
		var node = new NodeHealthInfo { NodeId = "node-1" };

		// Assert
		node.NodeId.ShouldBe("node-1");
	}

	[Fact]
	public void AllowNodeName_ToBeSet()
	{
		// Arrange & Act
		var node = new NodeHealthInfo { NodeName = "es-node-1" };

		// Assert
		node.NodeName.ShouldBe("es-node-1");
	}

	[Fact]
	public void AllowIsHealthy_ToBeSetToTrue()
	{
		// Arrange & Act
		var node = new NodeHealthInfo { IsHealthy = true };

		// Assert
		node.IsHealthy.ShouldBeTrue();
	}

	[Fact]
	public void AllowCpuUsagePercent_ToBeSet()
	{
		// Arrange & Act
		var node = new NodeHealthInfo { CpuUsagePercent = 45.5 };

		// Assert
		node.CpuUsagePercent.ShouldBe(45.5);
	}

	[Fact]
	public void AllowMemoryUsagePercent_ToBeSet()
	{
		// Arrange & Act
		var node = new NodeHealthInfo { MemoryUsagePercent = 72.3 };

		// Assert
		node.MemoryUsagePercent.ShouldBe(72.3);
	}

	[Fact]
	public void AllowDiskUsagePercent_ToBeSet()
	{
		// Arrange & Act
		var node = new NodeHealthInfo { DiskUsagePercent = 60.0 };

		// Assert
		node.DiskUsagePercent.ShouldBe(60.0);
	}

	[Fact]
	public void AllowHeapUsagePercent_ToBeSet()
	{
		// Arrange & Act
		var node = new NodeHealthInfo { HeapUsagePercent = 55.8 };

		// Assert
		node.HeapUsagePercent.ShouldBe(55.8);
	}

	[Fact]
	public void AllowLoadAverage_ToBeSet()
	{
		// Arrange & Act
		var node = new NodeHealthInfo { LoadAverage = 2.5 };

		// Assert
		node.LoadAverage.ShouldBe(2.5);
	}

	[Fact]
	public void AllowErrorMessage_ToBeSet()
	{
		// Arrange & Act
		var node = new NodeHealthInfo { ErrorMessage = "Node unreachable" };

		// Assert
		node.ErrorMessage.ShouldBe("Node unreachable");
	}

	[Fact]
	public void AllowLastUpdated_ToBeSet()
	{
		// Arrange
		var timestamp = DateTime.UtcNow;

		// Act
		var node = new NodeHealthInfo { LastUpdated = timestamp };

		// Assert
		node.LastUpdated.ShouldBe(timestamp);
	}

	#endregion

	#region Instance Creation Tests

	[Fact]
	public void CreateNewInstance_WithDefaultConstructor()
	{
		// Act
		var node = new NodeHealthInfo();

		// Assert
		node.ShouldNotBeNull();
	}

	[Fact]
	public void CreateNewInstance_RepresentingHealthyNode()
	{
		// Arrange & Act
		var node = new NodeHealthInfo
		{
			NodeId = "abc123",
			NodeName = "es-master-1",
			IsHealthy = true,
			CpuUsagePercent = 25.0,
			MemoryUsagePercent = 60.0,
			DiskUsagePercent = 40.0,
			HeapUsagePercent = 50.0,
			LoadAverage = 1.2,
			LastUpdated = DateTime.UtcNow
		};

		// Assert
		node.IsHealthy.ShouldBeTrue();
		node.NodeId.ShouldBe("abc123");
		node.CpuUsagePercent.ShouldBe(25.0);
		node.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void CreateNewInstance_RepresentingUnhealthyNode()
	{
		// Arrange & Act
		var node = new NodeHealthInfo
		{
			NodeId = "xyz789",
			NodeName = "es-data-2",
			IsHealthy = false,
			CpuUsagePercent = 99.0,
			MemoryUsagePercent = 95.0,
			DiskUsagePercent = 95.0,
			HeapUsagePercent = 90.0,
			LoadAverage = 15.5,
			ErrorMessage = "Node under extreme load",
			LastUpdated = DateTime.UtcNow
		};

		// Assert
		node.IsHealthy.ShouldBeFalse();
		node.CpuUsagePercent.ShouldBe(99.0);
		node.ErrorMessage.ShouldNotBeNull();
	}

	#endregion

	#region Boundary Value Tests

	[Fact]
	public void AllowZeroCpuUsagePercent()
	{
		// Arrange & Act
		var node = new NodeHealthInfo { CpuUsagePercent = 0.0 };

		// Assert
		node.CpuUsagePercent.ShouldBe(0.0);
	}

	[Fact]
	public void AllowFullCpuUsagePercent()
	{
		// Arrange & Act
		var node = new NodeHealthInfo { CpuUsagePercent = 100.0 };

		// Assert
		node.CpuUsagePercent.ShouldBe(100.0);
	}

	[Fact]
	public void AllowZeroLoadAverage()
	{
		// Arrange & Act
		var node = new NodeHealthInfo { LoadAverage = 0.0 };

		// Assert
		node.LoadAverage.ShouldBe(0.0);
	}

	[Fact]
	public void AllowHighLoadAverage()
	{
		// Arrange & Act
		var node = new NodeHealthInfo { LoadAverage = 100.0 };

		// Assert
		node.LoadAverage.ShouldBe(100.0);
	}

	#endregion
}
