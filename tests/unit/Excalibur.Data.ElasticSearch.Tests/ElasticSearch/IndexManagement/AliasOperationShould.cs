// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for the <see cref="AliasOperation"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.2): IndexManagement unit tests.
/// Tests verify required properties and optional configuration.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "IndexManagement")]
public sealed class AliasOperationShould
{
	#region Required Property Tests

	[Fact]
	public void OperationType_IsRequired()
	{
		// Arrange & Act
		var operation = new AliasOperation
		{
			OperationType = AliasOperationType.Add,
			AliasName = "events-read",
			IndexName = "events-000001"
		};

		// Assert
		operation.OperationType.ShouldBe(AliasOperationType.Add);
	}

	[Fact]
	public void AliasName_IsRequired()
	{
		// Arrange & Act
		var operation = new AliasOperation
		{
			OperationType = AliasOperationType.Add,
			AliasName = "events-read",
			IndexName = "events-000001"
		};

		// Assert
		operation.AliasName.ShouldBe("events-read");
	}

	[Fact]
	public void IndexName_IsRequired()
	{
		// Arrange & Act
		var operation = new AliasOperation
		{
			OperationType = AliasOperationType.Add,
			AliasName = "events-read",
			IndexName = "events-000001"
		};

		// Assert
		operation.IndexName.ShouldBe("events-000001");
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void AliasConfiguration_DefaultsToNull()
	{
		// Arrange & Act
		var operation = new AliasOperation
		{
			OperationType = AliasOperationType.Add,
			AliasName = "test",
			IndexName = "index-1"
		};

		// Assert
		operation.AliasConfiguration.ShouldBeNull();
	}

	#endregion

	#region Add Operation Tests

	[Fact]
	public void AddOperation_HasCorrectProperties()
	{
		// Arrange & Act
		var operation = new AliasOperation
		{
			OperationType = AliasOperationType.Add,
			AliasName = "events-write",
			IndexName = "events-000001"
		};

		// Assert
		operation.OperationType.ShouldBe(AliasOperationType.Add);
		operation.AliasName.ShouldBe("events-write");
		operation.IndexName.ShouldBe("events-000001");
	}

	#endregion

	#region Remove Operation Tests

	[Fact]
	public void RemoveOperation_HasCorrectProperties()
	{
		// Arrange & Act
		var operation = new AliasOperation
		{
			OperationType = AliasOperationType.Remove,
			AliasName = "events-write",
			IndexName = "events-000001"
		};

		// Assert
		operation.OperationType.ShouldBe(AliasOperationType.Remove);
		operation.AliasName.ShouldBe("events-write");
		operation.IndexName.ShouldBe("events-000001");
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllProperties_CanBeInitialized()
	{
		// Arrange & Act
		var operation = new AliasOperation
		{
			OperationType = AliasOperationType.Add,
			AliasName = "events-read",
			IndexName = "events-000001",
			AliasConfiguration = new Elastic.Clients.Elasticsearch.IndexManagement.Alias()
		};

		// Assert
		operation.OperationType.ShouldBe(AliasOperationType.Add);
		operation.AliasName.ShouldBe("events-read");
		operation.IndexName.ShouldBe("events-000001");
		operation.AliasConfiguration.ShouldNotBeNull();
	}

	#endregion
}
