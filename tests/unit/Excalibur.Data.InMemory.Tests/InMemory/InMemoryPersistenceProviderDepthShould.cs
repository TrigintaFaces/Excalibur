// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.InMemory;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Depth tests for <see cref="InMemoryPersistenceProvider"/>.
/// Covers Store/Retrieve/Remove, transactions, collections, metadata, GetService, and disposal.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryPersistenceProviderDepthShould : IDisposable
{
	private readonly InMemoryPersistenceProvider _provider;

	public InMemoryPersistenceProviderDepthShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryProviderOptions
		{
			Name = "test-provider",
			MaxItemsPerCollection = 100,
		});
		_provider = new InMemoryPersistenceProvider(options, NullLogger<InMemoryPersistenceProvider>.Instance);
	}

	[Fact]
	public void HaveCorrectNameAndProviderType()
	{
		// Assert
		_provider.Name.ShouldBe("test-provider");
		_provider.ProviderType.ShouldBe("InMemory");
		_provider.ConnectionString.ShouldBe("InMemory:test-provider");
	}

	[Fact]
	public void NotBeReadOnlyByDefault()
	{
		// Assert
		_provider.IsReadOnly.ShouldBeFalse();
	}

	[Fact]
	public void BeAvailableWhenNotDisposed()
	{
		// Assert
		_provider.IsAvailable.ShouldBeTrue();
	}

	[Fact]
	public void HaveRetryPolicy()
	{
		// Assert
		_provider.RetryPolicy.ShouldNotBeNull();
	}

	[Fact]
	public void StoreAndRetrieveItems()
	{
		// Arrange & Act
		_provider.Store("users", "user1", new TestUser("Alice", 30));

		// Assert
		var retrieved = _provider.Retrieve<TestUser>("users", "user1");
		retrieved.ShouldNotBeNull();
		retrieved!.Name.ShouldBe("Alice");
		retrieved.Age.ShouldBe(30);
	}

	[Fact]
	public void ReturnDefaultWhenItemNotFound()
	{
		// Act
		var result = _provider.Retrieve<TestUser>("users", "nonexistent");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void RemoveExistingItem()
	{
		// Arrange
		_provider.Store("users", "user1", new TestUser("Alice", 30));

		// Act
		var removed = _provider.Remove("users", "user1");

		// Assert
		removed.ShouldBeTrue();
		_provider.Retrieve<TestUser>("users", "user1").ShouldBeNull();
	}

	[Fact]
	public void ReturnFalseWhenRemovingNonExistentItem()
	{
		// Act
		var removed = _provider.Remove("users", "nonexistent");

		// Assert
		removed.ShouldBeFalse();
	}

	[Fact]
	public void ClearAllData()
	{
		// Arrange
		_provider.Store("users", "u1", new TestUser("Alice", 30));
		_provider.Store("orders", "o1", "order-data");

		// Act
		_provider.Clear();

		// Assert
		_provider.Retrieve<TestUser>("users", "u1").ShouldBeNull();
		_provider.Retrieve<string>("orders", "o1").ShouldBeNull();
	}

	[Fact]
	public void ThrowWhenStoringToReadOnlyProvider()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryProviderOptions
		{
			Name = "readonly",
			IsReadOnly = true,
		});
		var readOnlyProvider = new InMemoryPersistenceProvider(options, NullLogger<InMemoryPersistenceProvider>.Instance);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			readOnlyProvider.Store("users", "u1", new TestUser("Alice", 30)));
		readOnlyProvider.Dispose();
	}

	[Fact]
	public void ThrowWhenRemovingFromReadOnlyProvider()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryProviderOptions
		{
			Name = "readonly",
			IsReadOnly = true,
		});
		var readOnlyProvider = new InMemoryPersistenceProvider(options, NullLogger<InMemoryPersistenceProvider>.Instance);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			readOnlyProvider.Remove("users", "u1"));
		readOnlyProvider.Dispose();
	}

	[Fact]
	public void ThrowWhenClearingReadOnlyProvider()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryProviderOptions
		{
			Name = "readonly",
			IsReadOnly = true,
		});
		var readOnlyProvider = new InMemoryPersistenceProvider(options, NullLogger<InMemoryPersistenceProvider>.Instance);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => readOnlyProvider.Clear());
		readOnlyProvider.Dispose();
	}

	[Fact]
	public void ThrowWhenCollectionAtCapacity()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryProviderOptions
		{
			Name = "limited",
			MaxItemsPerCollection = 2,
		});
		var limitedProvider = new InMemoryPersistenceProvider(options, NullLogger<InMemoryPersistenceProvider>.Instance);
		limitedProvider.Store("col", "key1", "val1");
		limitedProvider.Store("col", "key2", "val2");

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			limitedProvider.Store("col", "key3", "val3"));
		limitedProvider.Dispose();
	}

	[Fact]
	public void GetOrCreateCollections()
	{
		// Act
		var collection1 = _provider.GetCollection("test-col");
		var collection2 = _provider.GetCollection("test-col");

		// Assert
		collection1.ShouldBeSameAs(collection2);
	}

	[Fact]
	public void CreateConnection()
	{
		// Act
		using var connection = _provider.CreateConnection();

		// Assert
		connection.ShouldNotBeNull();
		connection.Database.ShouldBe("test-provider");
	}

	[Fact]
	public async Task CreateConnectionAsync()
	{
		// Act
		using var connection = await _provider.CreateConnectionAsync(CancellationToken.None);

		// Assert
		connection.ShouldNotBeNull();
	}

	[Fact]
	public void BeginTransaction()
	{
		// Act
		using var transaction = _provider.BeginTransaction();

		// Assert
		transaction.ShouldNotBeNull();
		transaction.IsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
	}

	[Fact]
	public void BeginTransactionWithIsolationLevel()
	{
		// Act
		using var transaction = _provider.BeginTransaction(IsolationLevel.Serializable);

		// Assert
		transaction.IsolationLevel.ShouldBe(IsolationLevel.Serializable);
	}

	[Fact]
	public async Task BeginTransactionAsync()
	{
		// Act
		using var transaction = await _provider.BeginTransactionAsync(IsolationLevel.ReadCommitted, CancellationToken.None);

		// Assert
		transaction.ShouldNotBeNull();
	}

	[Fact]
	public async Task TestConnectionSuccessfully()
	{
		// Act
		var result = await _provider.TestConnectionAsync(CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task GetMetricsAsync()
	{
		// Arrange
		_provider.Store("col1", "k1", "v1");
		_provider.Store("col2", "k1", "v1");

		// Act
		var metrics = await _provider.GetMetricsAsync(CancellationToken.None);

		// Assert
		metrics["Provider"].ShouldBe("InMemory");
		metrics["Name"].ShouldBe("test-provider");
		metrics["Collections"].ShouldBe(2);
		((int)metrics["TotalItems"]).ShouldBe(2);
		metrics["IsAvailable"].ShouldBe(true);
	}

	[Fact]
	public void GetMetadata()
	{
		// Act
		var metadata = _provider.GetMetadata();

		// Assert
		metadata["Provider"].ShouldBe("InMemory");
		metadata["Name"].ShouldBe("test-provider");
		metadata["MaxItemsPerCollection"].ShouldBe(100);
	}

	[Fact]
	public async Task GetConnectionPoolStatsReturnNull()
	{
		// Act
		var stats = await _provider.GetConnectionPoolStatsAsync(CancellationToken.None);

		// Assert
		stats.ShouldBeNull();
	}

	[Fact]
	public void GetServiceReturnHealthInterface()
	{
		// Act
		var service = _provider.GetService(typeof(IPersistenceProviderHealth));

		// Assert
		service.ShouldBeSameAs(_provider);
	}

	[Fact]
	public void GetServiceReturnTransactionInterface()
	{
		// Act
		var service = _provider.GetService(typeof(IPersistenceProviderTransaction));

		// Assert
		service.ShouldBeSameAs(_provider);
	}

	[Fact]
	public void GetServiceReturnNullForUnknownType()
	{
		// Act
		var service = _provider.GetService(typeof(string));

		// Assert
		service.ShouldBeNull();
	}

	[Fact]
	public void GetServiceThrowWhenTypeIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _provider.GetService(null!));
	}

	[Fact]
	public void CreateTransactionScope()
	{
		// Act
		using var scope = _provider.CreateTransactionScope();

		// Assert
		scope.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowAfterDispose()
	{
		// Arrange
		_provider.Dispose();

		// Act & Assert
		Should.Throw<ObjectDisposedException>(() => _provider.Store("col", "key", "val"));
		Should.Throw<ObjectDisposedException>(() => _provider.Retrieve<string>("col", "key"));
		Should.Throw<ObjectDisposedException>(() => _provider.Remove("col", "key"));
		Should.Throw<ObjectDisposedException>(() => _provider.Clear());
		Should.Throw<ObjectDisposedException>(() => _provider.GetCollection("col"));
		Should.Throw<ObjectDisposedException>(() => _provider.CreateConnection());
	}

	[Fact]
	public void DisposeIdempotently()
	{
		// Act - should not throw
		_provider.Dispose();
		_provider.Dispose();

		// Assert
		_provider.IsAvailable.ShouldBeFalse();
	}

	[Fact]
	public void UseDefaultNameWhenNull()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryProviderOptions { Name = null });
		var provider = new InMemoryPersistenceProvider(options, NullLogger<InMemoryPersistenceProvider>.Instance);

		// Assert
		provider.Name.ShouldBe("inmemory");
		provider.Dispose();
	}

	public void Dispose()
	{
		_provider.Dispose();
	}

	private sealed record TestUser(string Name, int Age);
}
