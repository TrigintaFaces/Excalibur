// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Snapshots;

using Microsoft.Extensions.Options;

using MongoDB.Driver;

using Excalibur.Data.MongoDB;

namespace Excalibur.Data.Tests.MongoDB.Snapshots;

/// <summary>
/// Unit tests for the <see cref="MongoDbSnapshotStore"/> dual-constructor pattern.
/// Verifies both simple (options-based) and advanced (IMongoClient) constructors.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MongoDbSnapshotStoreConstructorShould : UnitTestBase
{
	private readonly ILogger<MongoDbSnapshotStore> _logger;
	private readonly IOptions<MongoDbSnapshotStoreOptions> _options;

	public MongoDbSnapshotStoreConstructorShould()
	{
		_logger = A.Fake<ILogger<MongoDbSnapshotStore>>();
		_options = Options.Create(new MongoDbSnapshotStoreOptions());
	}

	#region Simple Constructor Tests

	[Fact]
	public void SimpleConstructor_WithValidOptions_CreatesInstance()
	{
		// Arrange & Act
		var store = new MongoDbSnapshotStore(_options, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void SimpleConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbSnapshotStore(options: null!, _logger));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void SimpleConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbSnapshotStore(_options, logger: null!));
		exception.ParamName.ShouldBe("logger");
	}

	[Fact]
	public void SimpleConstructor_WithInvalidOptions_ThrowsInvalidOperationException()
	{
		// Arrange
		var invalidOptions = Options.Create(new MongoDbSnapshotStoreOptions
		{
			ConnectionString = string.Empty
		});

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MongoDbSnapshotStore(invalidOptions, _logger));
	}

	#endregion Simple Constructor Tests

	#region Client Constructor Tests

	[Fact]
	public void ClientConstructor_WithValidClient_CreatesInstance()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();
		var database = A.Fake<IMongoDatabase>();
		var collection = A.Fake<IMongoCollection<object>>();

		_ = A.CallTo(() => client.GetDatabase(_options.Value.DatabaseName, null))
			.Returns(database);

		// Act
		var store = new MongoDbSnapshotStore(client, _options, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void ClientConstructor_WithNullClient_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbSnapshotStore(client: null!, _options, _logger));
		exception.ParamName.ShouldBe("client");
	}

	[Fact]
	public void ClientConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbSnapshotStore(client, options: null!, _logger));
		exception.ParamName.ShouldBe("options");
	}

	[Fact]
	public void ClientConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			new MongoDbSnapshotStore(client, _options, logger: null!));
		exception.ParamName.ShouldBe("logger");
	}

	#endregion Client Constructor Tests

	#region Constructor Equivalence Tests

	[Fact]
	public void BothConstructors_CreateValidInstances()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();
		var database = A.Fake<IMongoDatabase>();

		_ = A.CallTo(() => client.GetDatabase(_options.Value.DatabaseName, null))
			.Returns(database);

		// Act
		var simpleStore = new MongoDbSnapshotStore(_options, _logger);
		var clientStore = new MongoDbSnapshotStore(client, _options, _logger);

		// Assert - Both create valid instances
		_ = simpleStore.ShouldNotBeNull();
		_ = clientStore.ShouldNotBeNull();
	}

	#endregion Constructor Equivalence Tests

	#region Dispose Tests

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Arrange
		var store = new MongoDbSnapshotStore(_options, _logger);

		// Act & Assert - Should not throw
		await store.DisposeAsync();
		await store.DisposeAsync();
		await store.DisposeAsync();
	}

	#endregion Dispose Tests
}
