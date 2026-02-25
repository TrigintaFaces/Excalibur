// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="IndexHealthStatus"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify property initialization and defaults.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class IndexHealthStatusShould
{
	#region Required Property Tests

	[Fact]
	public void IndexName_IsRequired()
	{
		// Arrange & Act
		var status = new IndexHealthStatus
		{
			IndexName = "my-index",
			Status = "green"
		};

		// Assert
		status.IndexName.ShouldBe("my-index");
	}

	[Fact]
	public void Status_IsRequired()
	{
		// Arrange & Act
		var status = new IndexHealthStatus
		{
			IndexName = "my-index",
			Status = "yellow"
		};

		// Assert
		status.Status.ShouldBe("yellow");
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void PrimaryShards_DefaultsToZero()
	{
		// Arrange & Act
		var status = new IndexHealthStatus
		{
			IndexName = "test",
			Status = "green"
		};

		// Assert
		status.PrimaryShards.ShouldBe(0);
	}

	[Fact]
	public void ReplicaShards_DefaultsToZero()
	{
		// Arrange & Act
		var status = new IndexHealthStatus
		{
			IndexName = "test",
			Status = "green"
		};

		// Assert
		status.ReplicaShards.ShouldBe(0);
	}

	[Fact]
	public void DocumentCount_DefaultsToZero()
	{
		// Arrange & Act
		var status = new IndexHealthStatus
		{
			IndexName = "test",
			Status = "green"
		};

		// Assert
		status.DocumentCount.ShouldBe(0);
	}

	[Fact]
	public void TotalSize_DefaultsToNull()
	{
		// Arrange & Act
		var status = new IndexHealthStatus
		{
			IndexName = "test",
			Status = "green"
		};

		// Assert
		status.TotalSize.ShouldBeNull();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange & Act
		var status = new IndexHealthStatus
		{
			IndexName = "events-2026.02",
			Status = "green",
			PrimaryShards = 5,
			ReplicaShards = 1,
			DocumentCount = 1_000_000,
			TotalSize = "10.5GB"
		};

		// Assert
		status.IndexName.ShouldBe("events-2026.02");
		status.Status.ShouldBe("green");
		status.PrimaryShards.ShouldBe(5);
		status.ReplicaShards.ShouldBe(1);
		status.DocumentCount.ShouldBe(1_000_000);
		status.TotalSize.ShouldBe("10.5GB");
	}

	#endregion

	#region Health Status Value Tests

	[Theory]
	[InlineData("green")]
	[InlineData("yellow")]
	[InlineData("red")]
	public void Status_AcceptsValidValues(string healthStatus)
	{
		// Arrange & Act
		var status = new IndexHealthStatus
		{
			IndexName = "test",
			Status = healthStatus
		};

		// Assert
		status.Status.ShouldBe(healthStatus);
	}

	#endregion
}
