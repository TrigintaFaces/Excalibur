// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Azure.Cosmos;

namespace Excalibur.Data.CosmosDb.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class CosmosDbRepositoryBaseShould : UnitTestBase
{
	#region Constructor Validation Tests

	[Fact]
	public void ThrowWhenClientIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new TestCosmosDbRepository(null!, "testdb", "testcontainer"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowWhenDatabaseNameIsNullOrWhitespace(string? databaseName)
	{
		// Arrange
		var client = A.Fake<CosmosClient>();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => new TestCosmosDbRepository(client, databaseName!, "testcontainer"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowWhenContainerNameIsNullOrWhitespace(string? containerName)
	{
		// Arrange
		var client = A.Fake<CosmosClient>();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => new TestCosmosDbRepository(client, "testdb", containerName!));
	}

	[Fact]
	public void ConstructSuccessfully_WithValidParameters()
	{
		// Arrange
		var client = A.Fake<CosmosClient>();

		// Act
		var repo = new TestCosmosDbRepository(client, "testdb", "testcontainer");

		// Assert
		repo.ShouldNotBeNull();
		repo.ExposedContainerName.ShouldBe("testcontainer");
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementICosmosDbRepositoryBase()
	{
		// Arrange
		var client = A.Fake<CosmosClient>();

		// Act
		var repo = new TestCosmosDbRepository(client, "testdb", "testcontainer");

		// Assert
		repo.ShouldBeAssignableTo<ICosmosDbRepositoryBase<SampleDocument>>();
	}

	[Fact]
	public void ImplementICosmosDbRepositoryBaseQuery()
	{
		// Arrange
		var client = A.Fake<CosmosClient>();

		// Act
		var repo = new TestCosmosDbRepository(client, "testdb", "testcontainer");

		// Assert
		repo.ShouldBeAssignableTo<ICosmosDbRepositoryBaseQuery<SampleDocument>>();
	}

	#endregion

	#region Test Helpers

	private sealed class TestCosmosDbRepository : CosmosDbRepositoryBase<SampleDocument>
	{
		public TestCosmosDbRepository(CosmosClient client, string databaseName, string containerName)
			: base(client, databaseName, containerName)
		{
		}

		public string ExposedContainerName => ContainerName;

		public override Task InitializeContainerAsync(CancellationToken cancellationToken)
			=> Task.CompletedTask;
	}

	private sealed class SampleDocument
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
	}

	#endregion
}
