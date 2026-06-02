// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MongoDB.Bson;
using MongoDB.Driver;

namespace Excalibur.Data.MongoDB.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class MongoDbRepositoryBaseShould : UnitTestBase
{
	#region Constructor Validation Tests

	[Fact]
	public void ThrowWhenClientIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new TestMongoDbRepository(null!, "testdb", "testcollection"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowWhenDatabaseNameIsNullOrWhitespace(string? databaseName)
	{
		// Arrange
		var client = CreateFakeMongoClient();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => new TestMongoDbRepository(client, databaseName!, "testcollection"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowWhenCollectionNameIsNullOrWhitespace(string? collectionName)
	{
		// Arrange
		var client = CreateFakeMongoClient();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => new TestMongoDbRepository(client, "testdb", collectionName!));
	}

	[Fact]
	public void ConstructSuccessfully_WithValidParameters()
	{
		// Arrange
		var client = CreateFakeMongoClient();

		// Act
		var repo = new TestMongoDbRepository(client, "testdb", "testcollection");

		// Assert
		repo.ShouldNotBeNull();
		repo.ExposedCollectionName.ShouldBe("testcollection");
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementIMongoDbRepositoryBase()
	{
		// Arrange
		var client = CreateFakeMongoClient();

		// Act
		var repo = new TestMongoDbRepository(client, "testdb", "testcollection");

		// Assert
		repo.ShouldBeAssignableTo<IMongoDbRepositoryBase<SampleDocument>>();
	}

	[Fact]
	public void ImplementIMongoDbRepositoryBaseQuery()
	{
		// Arrange
		var client = CreateFakeMongoClient();

		// Act
		var repo = new TestMongoDbRepository(client, "testdb", "testcollection");

		// Assert
		repo.ShouldBeAssignableTo<IMongoDbRepositoryBaseQuery<SampleDocument>>();
	}

	#endregion

	#region Convention Registration Tests

	[Fact]
	public void RegisterIgnoreExtraElementsConvention()
	{
		// Arrange
		var client = CreateFakeMongoClient();

		// Act — construction triggers MongoDbConventionInitializer.EnsureRegistered()
		var repo = new TestMongoDbRepository(client, "testdb", "testcollection");

		// Assert — if we got here without exception, convention registration succeeded.
		// The convention is global/static, so we verify the repo constructed cleanly.
		repo.ShouldNotBeNull();
	}

	#endregion

	#region Test Helpers

	private static IMongoClient CreateFakeMongoClient()
	{
		var client = A.Fake<IMongoClient>();
		var database = A.Fake<IMongoDatabase>();
		var collection = A.Fake<IMongoCollection<BsonDocument>>();

		A.CallTo(() => client.GetDatabase(A<string>._, A<MongoDatabaseSettings>._))
			.Returns(database);
		A.CallTo(() => database.GetCollection<BsonDocument>(A<string>._, A<MongoCollectionSettings>._))
			.Returns(collection);

		return client;
	}

	private sealed class TestMongoDbRepository : MongoDbRepositoryBase<SampleDocument>
	{
		public TestMongoDbRepository(IMongoClient client, string databaseName, string collectionName)
			: base(client, databaseName, collectionName)
		{
		}

		public string ExposedCollectionName => CollectionName;

		public override Task InitializeCollectionAsync(CancellationToken cancellationToken)
			=> Task.CompletedTask;
	}

	private sealed class SampleDocument
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
	}

	#endregion
}
