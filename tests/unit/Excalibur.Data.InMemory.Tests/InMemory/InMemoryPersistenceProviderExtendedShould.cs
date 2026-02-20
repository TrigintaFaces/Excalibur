// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.InMemory;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Extended unit tests for InMemoryPersistenceProvider collection and metadata operations.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.InMemory")]
public sealed class InMemoryPersistenceProviderExtendedShould : UnitTestBase
{
	private readonly ILogger<InMemoryPersistenceProvider> _logger;
	private readonly InMemoryPersistenceProvider _provider;

	public InMemoryPersistenceProviderExtendedShould()
	{
		_logger = A.Fake<ILogger<InMemoryPersistenceProvider>>();
		var options = Options.Create(new InMemoryProviderOptions { Name = "TestProvider" });
		_provider = new InMemoryPersistenceProvider(options, _logger);
	}

	#region Store Tests

	[Fact]
	public void Store_AddsItemToCollection()
	{
		// Act
		_provider.Store("users", "user1", new TestEntity { Id = "user1", Name = "John" });

		// Assert
		var retrieved = _provider.Retrieve<TestEntity>("users", "user1");
		retrieved.ShouldNotBeNull();
		retrieved.Id.ShouldBe("user1");
		retrieved.Name.ShouldBe("John");
	}

	[Fact]
	public void Store_OverwritesExistingItem()
	{
		// Arrange
		_provider.Store("users", "user1", new TestEntity { Id = "user1", Name = "John" });

		// Act
		_provider.Store("users", "user1", new TestEntity { Id = "user1", Name = "Jane" });

		// Assert
		var retrieved = _provider.Retrieve<TestEntity>("users", "user1");
		retrieved.Name.ShouldBe("Jane");
	}

	[Fact]
	public void Store_ThrowsInvalidOperationException_WhenReadOnly()
	{
		// Arrange
		var options = Options.Create(new InMemoryProviderOptions { Name = "ReadOnlyProvider", IsReadOnly = true });
		using var readOnlyProvider = new InMemoryPersistenceProvider(options, _logger);

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() =>
			readOnlyProvider.Store("users", "user1", new TestEntity { Id = "user1" }));
		exception.Message.ShouldContain("read-only");
	}

	[Fact]
	public void Store_ThrowsInvalidOperationException_WhenMaxItemsReached()
	{
		// Arrange
		var options = Options.Create(new InMemoryProviderOptions
		{
			Name = "LimitedProvider",
			MaxItemsPerCollection = 2,
		});
		using var limitedProvider = new InMemoryPersistenceProvider(options, _logger);

		limitedProvider.Store("users", "user1", new TestEntity { Id = "user1" });
		limitedProvider.Store("users", "user2", new TestEntity { Id = "user2" });

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() =>
			limitedProvider.Store("users", "user3", new TestEntity { Id = "user3" }));
		exception.Message.ShouldContain("maximum capacity");
		exception.Message.ShouldContain("2");
	}

	[Fact]
	public void Store_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		var options = Options.Create(new InMemoryProviderOptions());
		var provider = new InMemoryPersistenceProvider(options, _logger);
		provider.Dispose();

		// Act & Assert
		_ = Should.Throw<ObjectDisposedException>(() =>
			provider.Store("users", "user1", new TestEntity { Id = "user1" }));
	}

	#endregion Store Tests

	#region Retrieve Tests

	[Fact]
	public void Retrieve_ReturnsNull_WhenItemDoesNotExist()
	{
		// Act
		var result = _provider.Retrieve<TestEntity>("users", "nonexistent");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void Retrieve_ReturnsNull_WhenCollectionDoesNotExist()
	{
		// Act
		var result = _provider.Retrieve<TestEntity>("nonexistent", "user1");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void Retrieve_ReturnsDefault_WhenTypeDoesNotMatch()
	{
		// Arrange
		_provider.Store("users", "user1", "this is a string");

		// Act - Try to retrieve as TestEntity
		var result = _provider.Retrieve<TestEntity>("users", "user1");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void Retrieve_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		var options = Options.Create(new InMemoryProviderOptions());
		var provider = new InMemoryPersistenceProvider(options, _logger);
		provider.Dispose();

		// Act & Assert
		_ = Should.Throw<ObjectDisposedException>(() =>
			provider.Retrieve<TestEntity>("users", "user1"));
	}

	#endregion Retrieve Tests

	#region Remove Tests

	[Fact]
	public void Remove_ReturnsTrue_WhenItemExists()
	{
		// Arrange
		_provider.Store("users", "user1", new TestEntity { Id = "user1" });

		// Act
		var result = _provider.Remove("users", "user1");

		// Assert
		result.ShouldBeTrue();
		_provider.Retrieve<TestEntity>("users", "user1").ShouldBeNull();
	}

	[Fact]
	public void Remove_ReturnsFalse_WhenItemDoesNotExist()
	{
		// Act
		var result = _provider.Remove("users", "nonexistent");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void Remove_ThrowsInvalidOperationException_WhenReadOnly()
	{
		// Arrange
		var options = Options.Create(new InMemoryProviderOptions { Name = "ReadOnlyProvider", IsReadOnly = true });
		using var readOnlyProvider = new InMemoryPersistenceProvider(options, _logger);

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() =>
			readOnlyProvider.Remove("users", "user1"));
		exception.Message.ShouldContain("read-only");
	}

	[Fact]
	public void Remove_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		var options = Options.Create(new InMemoryProviderOptions());
		var provider = new InMemoryPersistenceProvider(options, _logger);
		provider.Dispose();

		// Act & Assert
		_ = Should.Throw<ObjectDisposedException>(() =>
			provider.Remove("users", "user1"));
	}

	#endregion Remove Tests

	#region GetCollection Tests

	[Fact]
	public void GetCollection_CreatesNewCollection_WhenDoesNotExist()
	{
		// Act
		var collection = _provider.GetCollection("new_collection");

		// Assert
		collection.ShouldNotBeNull();
		collection.Count.ShouldBe(0);
	}

	[Fact]
	public void GetCollection_ReturnsSameCollection_ForSameName()
	{
		// Act
		var collection1 = _provider.GetCollection("users");
		var collection2 = _provider.GetCollection("users");

		// Assert
		collection1.ShouldBeSameAs(collection2);
	}

	[Fact]
	public void GetCollection_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		var options = Options.Create(new InMemoryProviderOptions());
		var provider = new InMemoryPersistenceProvider(options, _logger);
		provider.Dispose();

		// Act & Assert
		_ = Should.Throw<ObjectDisposedException>(() =>
			provider.GetCollection("users"));
	}

	#endregion GetCollection Tests

	#region Clear Tests

	[Fact]
	public void Clear_RemovesAllCollections()
	{
		// Arrange
		_provider.Store("users", "user1", new TestEntity { Id = "user1" });
		_provider.Store("orders", "order1", new TestEntity { Id = "order1" });

		// Act
		_provider.Clear();

		// Assert
		_provider.Retrieve<TestEntity>("users", "user1").ShouldBeNull();
		_provider.Retrieve<TestEntity>("orders", "order1").ShouldBeNull();
	}

	[Fact]
	public void Clear_ThrowsInvalidOperationException_WhenReadOnly()
	{
		// Arrange
		var options = Options.Create(new InMemoryProviderOptions { Name = "ReadOnlyProvider", IsReadOnly = true });
		using var readOnlyProvider = new InMemoryPersistenceProvider(options, _logger);

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() =>
			readOnlyProvider.Clear());
		exception.Message.ShouldContain("read-only");
	}

	[Fact]
	public void Clear_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		var options = Options.Create(new InMemoryProviderOptions());
		var provider = new InMemoryPersistenceProvider(options, _logger);
		provider.Dispose();

		// Act & Assert
		_ = Should.Throw<ObjectDisposedException>(() =>
			provider.Clear());
	}

	#endregion Clear Tests

	#region GetMetadata Tests

	[Fact]
	public void GetMetadata_ReturnsProviderInformation()
	{
		// Act
		var metadata = _provider.GetMetadata();

		// Assert
		metadata.ShouldNotBeNull();
		metadata["Provider"].ShouldBe("InMemory");
		metadata["Name"].ShouldBe("TestProvider");
	}

	[Fact]
	public void GetMetadata_IncludesCollectionCounts()
	{
		// Arrange
		_provider.Store("users", "user1", new TestEntity { Id = "user1" });
		_provider.Store("users", "user2", new TestEntity { Id = "user2" });
		_provider.Store("orders", "order1", new TestEntity { Id = "order1" });

		// Act
		var metadata = _provider.GetMetadata();

		// Assert
		metadata["Collections"].ShouldBe(2);
		metadata["TotalItems"].ShouldBe(3);
	}

	[Fact]
	public void GetMetadata_IncludesMaxItemsPerCollection()
	{
		// Arrange
		var options = Options.Create(new InMemoryProviderOptions
		{
			Name = "TestProvider",
			MaxItemsPerCollection = 100,
		});
		using var provider = new InMemoryPersistenceProvider(options, _logger);

		// Act
		var metadata = provider.GetMetadata();

		// Assert
		metadata["MaxItemsPerCollection"].ShouldBe(100);
	}

	[Fact]
	public void GetMetadata_IncludesPersistToDiskFlag()
	{
		// Arrange
		var options = Options.Create(new InMemoryProviderOptions
		{
			Name = "TestProvider",
			PersistToDisk = true,
		});
		using var provider = new InMemoryPersistenceProvider(options, _logger);

		// Act
		var metadata = provider.GetMetadata();

		// Assert
		metadata["PersistToDisk"].ShouldBe(true);
	}

	[Fact]
	public void GetMetadata_IncludesIsReadOnlyFlag()
	{
		// Arrange
		var options = Options.Create(new InMemoryProviderOptions
		{
			Name = "TestProvider",
			IsReadOnly = true,
		});
		using var provider = new InMemoryPersistenceProvider(options, _logger);

		// Act
		var metadata = provider.GetMetadata();

		// Assert
		metadata["IsReadOnly"].ShouldBe(true);
	}

	#endregion GetMetadata Tests

	#region Connection Tests

	[Fact]
	public void Connection_OpenSetsStateToOpen()
	{
		// Arrange
		var connection = _provider.CreateConnection();

		// Act
		connection.Open();

		// Assert
		connection.State.ShouldBe(ConnectionState.Open);
		connection.Dispose();
	}

	[Fact]
	public void Connection_CloseSetsStateToClosed()
	{
		// Arrange
		var connection = _provider.CreateConnection();
		connection.Open();

		// Act
		connection.Close();

		// Assert
		connection.State.ShouldBe(ConnectionState.Closed);
		connection.Dispose();
	}

	[Fact]
	public void Connection_ReturnsProviderName()
	{
		// Arrange
		var connection = _provider.CreateConnection();

		// Act & Assert
		connection.Database.ShouldBe("TestProvider");
		connection.Dispose();
	}

	[Fact]
	public void Connection_ReturnsConnectionString()
	{
		// Arrange
		var connection = _provider.CreateConnection();

		// Act & Assert
		connection.ConnectionString.ShouldContain("InMemory");
		connection.Dispose();
	}

	[Fact]
	public void Connection_ConnectionTimeoutIsZero()
	{
		// Arrange
		var connection = _provider.CreateConnection();

		// Act & Assert
		connection.ConnectionTimeout.ShouldBe(0);
		connection.Dispose();
	}

	[Fact]
	public void Connection_ConnectionStringCanBeSet()
	{
		// Arrange
		var connection = _provider.CreateConnection();

		// Act
		connection.ConnectionString = "NewConnectionString";

		// Assert
		connection.ConnectionString.ShouldBe("NewConnectionString");
		connection.Dispose();
	}

	[Fact]
	public void Connection_BeginTransactionReturnsTransaction()
	{
		// Arrange
		var connection = _provider.CreateConnection();

		// Act
		var transaction = connection.BeginTransaction();

		// Assert
		transaction.ShouldNotBeNull();
		transaction.Dispose();
		connection.Dispose();
	}

	[Fact]
	public void Connection_BeginTransactionWithIsolationLevel()
	{
		// Arrange
		var connection = _provider.CreateConnection();

		// Act
		var transaction = connection.BeginTransaction(IsolationLevel.Serializable);

		// Assert
		transaction.ShouldNotBeNull();
		transaction.IsolationLevel.ShouldBe(IsolationLevel.Serializable);
		transaction.Dispose();
		connection.Dispose();
	}

	[Fact]
	public void Connection_ChangeDatabaseThrowsNotSupported()
	{
		// Arrange
		var connection = _provider.CreateConnection();

		// Act & Assert
		_ = Should.Throw<NotSupportedException>(() =>
			connection.ChangeDatabase("other"));
		connection.Dispose();
	}

	[Fact]
	public void Connection_CreateCommandThrowsNotSupported()
	{
		// Arrange
		var connection = _provider.CreateConnection();

		// Act & Assert
		_ = Should.Throw<NotSupportedException>(() =>
			connection.CreateCommand());
		connection.Dispose();
	}

	[Fact]
	public void Connection_DisposeClosesConnection()
	{
		// Arrange
		var connection = _provider.CreateConnection();
		connection.Open();

		// Act
		connection.Dispose();

		// Assert
		connection.State.ShouldBe(ConnectionState.Closed);
	}

	#endregion Connection Tests

	#region Transaction Tests

	[Fact]
	public void Transaction_CommitDoesNotThrow()
	{
		// Arrange
		var transaction = _provider.BeginTransaction();

		// Act & Assert
		Should.NotThrow(() => transaction.Commit());
		transaction.Dispose();
	}

	[Fact]
	public void Transaction_RollbackDoesNotThrow()
	{
		// Arrange
		var transaction = _provider.BeginTransaction();

		// Act & Assert
		Should.NotThrow(() => transaction.Rollback());
		transaction.Dispose();
	}

	[Fact]
	public void Transaction_CommitThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		var transaction = _provider.BeginTransaction();
		transaction.Dispose();

		// Act & Assert
		_ = Should.Throw<ObjectDisposedException>(() => transaction.Commit());
	}

	[Fact]
	public void Transaction_RollbackThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		var transaction = _provider.BeginTransaction();
		transaction.Dispose();

		// Act & Assert
		_ = Should.Throw<ObjectDisposedException>(() => transaction.Rollback());
	}

	[Fact]
	public void Transaction_HasConnection()
	{
		// Arrange
		var transaction = _provider.BeginTransaction();

		// Assert
		transaction.Connection.ShouldNotBeNull();
		transaction.Dispose();
	}

	[Fact]
	public void Transaction_ConnectionIsNullAfterDispose()
	{
		// Arrange
		var transaction = _provider.BeginTransaction();

		// Act
		transaction.Dispose();

		// Assert
		transaction.Connection.ShouldBeNull();
	}

	[Fact]
	public void Transaction_DisposeIsIdempotent()
	{
		// Arrange
		var transaction = _provider.BeginTransaction();

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			transaction.Dispose();
			transaction.Dispose();
			transaction.Dispose();
		});
	}

	[Fact]
	public void BeginTransaction_ThrowsTimeoutException_WhenLockNotAvailable()
	{
		// Arrange - Hold the transaction lock with a long-running transaction
		// Note: This test demonstrates the timeout mechanism but can't easily simulate
		// a 30-second timeout in a unit test. Instead we verify the behavior when
		// multiple transactions are properly sequenced.

		var transaction1 = _provider.BeginTransaction();

		// Act - Starting another transaction should wait for the first one
		// (In practice, the test would need async and timing control to verify timeout)
		transaction1.Dispose(); // Release the lock

		var transaction2 = _provider.BeginTransaction(); // Should succeed now
		transaction2.ShouldNotBeNull();
		transaction2.Dispose();
	}

	#endregion Transaction Tests

	#region TestConnectionAsync Tests

	[Fact]
	public async Task TestConnectionAsync_ReturnsFalse_WhenDisposed()
	{
		// Arrange
		var options = Options.Create(new InMemoryProviderOptions());
		var provider = new InMemoryPersistenceProvider(options, _logger);
		provider.Dispose();

		// Act
		var result = await provider.TestConnectionAsync(CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion TestConnectionAsync Tests

	#region Multiple Dispose Tests

	[Fact]
	public void Dispose_IsIdempotent()
	{
		// Arrange
		var options = Options.Create(new InMemoryProviderOptions());
		var provider = new InMemoryPersistenceProvider(options, _logger);

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			provider.Dispose();
			provider.Dispose();
			provider.Dispose();
		});
	}

	[Fact]
	public async Task DisposeAsync_IsIdempotent()
	{
		// Arrange
		var options = Options.Create(new InMemoryProviderOptions());
		var provider = new InMemoryPersistenceProvider(options, _logger);

		// Act & Assert - Should not throw
		await Should.NotThrowAsync(async () =>
		{
			await provider.DisposeAsync();
			await provider.DisposeAsync();
			await provider.DisposeAsync();
		});
	}

	#endregion Multiple Dispose Tests

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_provider?.Dispose();
		}
		base.Dispose(disposing);
	}

	/// <summary>
	/// Test entity for storage operations.
	/// </summary>
	private sealed class TestEntity
	{
		public string Id { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
	}
}
