// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions.Persistence;

using MongoDB.Driver;

using Excalibur.Data.MongoDB;
namespace Excalibur.Data.Tests.MongoDB.Transactions;

/// <summary>
/// Unit tests for <see cref="MongoDbTransactionScope"/>.
/// Validates Sprint 392 implementation: MongoDB Transaction Support.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MongoDbTransactionScopeShould : IDisposable
{
	private readonly ILogger<MongoDbPersistenceProvider> _logger;
	private readonly MongoDbPersistenceProvider _provider;

	public MongoDbTransactionScopeShould()
	{
		_logger = A.Fake<ILogger<MongoDbPersistenceProvider>>();
		_provider = new MongoDbPersistenceProvider(_logger);
	}

	[Fact]
	public void InitializeWithActiveStatus()
	{
		// Arrange & Act
		using var scope = _provider.CreateTransactionScope();

		// Assert
		scope.Status.ShouldBe(TransactionStatus.Active);
	}

	[Fact]
	public void InitializeWithUniqueTransactionId()
	{
		// Arrange & Act
		using var scope1 = _provider.CreateTransactionScope();
		using var scope2 = _provider.CreateTransactionScope();

		// Assert
		scope1.TransactionId.ShouldNotBeNullOrEmpty();
		scope2.TransactionId.ShouldNotBeNullOrEmpty();
		scope1.TransactionId.ShouldNotBe(scope2.TransactionId);
	}

	[Fact]
	public void InitializeWithSpecifiedIsolationLevel()
	{
		// Arrange & Act
		using var scope = _provider.CreateTransactionScope(IsolationLevel.Serializable);

		// Assert
		scope.IsolationLevel.ShouldBe(IsolationLevel.Serializable);
	}

	[Fact]
	public void InitializeWithDefaultIsolationLevel()
	{
		// Arrange & Act
		using var scope = _provider.CreateTransactionScope();

		// Assert
		scope.IsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
	}

	[Fact]
	public void InitializeWithSpecifiedTimeout()
	{
		// Arrange
		var timeout = TimeSpan.FromMinutes(10);

		// Act
		using var scope = _provider.CreateTransactionScope(timeout: timeout);

		// Assert
		scope.Timeout.ShouldBe(timeout);
	}

	[Fact]
	public void InitializeWithDefaultTimeout()
	{
		// Arrange & Act
		using var scope = _provider.CreateTransactionScope();

		// Assert
		scope.Timeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void InitializeStartTimeAsUtcNow()
	{
		// Arrange
		var before = DateTime.UtcNow;

		// Act
		using var scope = _provider.CreateTransactionScope();
		var after = DateTime.UtcNow;

		// Assert
		scope.StartTime.ShouldBeGreaterThanOrEqualTo(before);
		scope.StartTime.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void SessionProperty_ReturnsNull_BeforeInitialization()
	{
		// Arrange & Act
		using var scope = _provider.CreateTransactionScope();

		// Assert - Cast to access internal Session property
		var mongoScope = scope as MongoDbTransactionScope;
		_ = mongoScope.ShouldNotBeNull();
		mongoScope.Session.ShouldBeNull();
	}

	[Fact]
	public void DatabaseProperty_ThrowsWhenProviderNotInitialized()
	{
		// Arrange & Act
		using var scope = _provider.CreateTransactionScope();

		// Assert - Cast to access internal Database property
		var mongoScope = scope as MongoDbTransactionScope;
		_ = mongoScope.ShouldNotBeNull();

		// Database property calls provider.GetDatabase() which throws when not initialized
		_ = Should.Throw<InvalidOperationException>(
			() => _ = mongoScope.Database);
	}

	[Fact]
	public async Task CommitAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		scope.Dispose();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(
			() => scope.CommitAsync(CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RollbackAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		scope.Dispose();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(
			() => scope.RollbackAsync(CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public void OnCommit_ThrowsArgumentNullException_WhenCallbackIsNull()
	{
		// Arrange
		using var scope = _provider.CreateTransactionScope();
		var callbacks = (ITransactionScopeCallbacks)scope;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => callbacks.OnCommit(null!));
	}

	[Fact]
	public void OnRollback_ThrowsArgumentNullException_WhenCallbackIsNull()
	{
		// Arrange
		using var scope = _provider.CreateTransactionScope();
		var callbacks = (ITransactionScopeCallbacks)scope;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => callbacks.OnRollback(null!));
	}

	[Fact]
	public void OnComplete_ThrowsArgumentNullException_WhenCallbackIsNull()
	{
		// Arrange
		using var scope = _provider.CreateTransactionScope();
		var callbacks = (ITransactionScopeCallbacks)scope;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => callbacks.OnComplete(null!));
	}

	[Fact]
	public void GetEnlistedProviders_ReturnsCreatingProvider()
	{
		// Arrange & Act
		using var scope = _provider.CreateTransactionScope();
		var providers = scope.GetEnlistedProviders().ToList();

		// Assert
		providers.Count.ShouldBe(1);
		providers[0].ShouldBe(_provider);
	}

	[Fact]
	public async Task EnlistProviderAsync_ThrowsNotSupportedException_ForDifferentProvider()
	{
		// Arrange
		using var scope = _provider.CreateTransactionScope();
		var otherProvider = A.Fake<IPersistenceProvider>();

		// Act & Assert
		_ = await Should.ThrowAsync<NotSupportedException>(
			() => scope.EnlistProviderAsync(otherProvider, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task EnlistProviderAsync_DoesNotThrow_ForSameProvider()
	{
		// Arrange
		using var scope = _provider.CreateTransactionScope();

		// Act & Assert
		await Should.NotThrowAsync(
			() => scope.EnlistProviderAsync(_provider, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task EnlistConnectionAsync_DoesNotThrow()
	{
		// Arrange
		using var scope = _provider.CreateTransactionScope();
		var connection = A.Fake<IDbConnection>();

		// Act & Assert - MongoDB handles connections through sessions
		await Should.NotThrowAsync(
			() => scope.EnlistConnectionAsync(connection, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public void DoesNotImplementITransactionScopeAdvanced()
	{
		// Arrange — MongoDB does not support savepoints or nested scopes
		using var scope = _provider.CreateTransactionScope();

		// Assert
		scope.ShouldNotBeAssignableTo<ITransactionScopeAdvanced>();
	}

	[Fact]
	public void ImplementsITransactionScopeCallbacks()
	{
		// Arrange
		using var scope = _provider.CreateTransactionScope();

		// Assert
		_ = scope.ShouldBeAssignableTo<ITransactionScopeCallbacks>();
	}

	[Fact]
	public void Dispose_DoesNotThrow()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();

		// Act & Assert
		Should.NotThrow(scope.Dispose);
	}

	[Fact]
	public void Dispose_IsIdempotent()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();

		// Act & Assert - Multiple disposes should not throw
		Should.NotThrow(() =>
		{
			scope.Dispose();
			scope.Dispose();
			scope.Dispose();
		});
	}

	[Fact]
	public async Task DisposeAsync_DoesNotThrow()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();

		// Act & Assert
		await Should.NotThrowAsync(
			() => scope.DisposeAsync().AsTask()).ConfigureAwait(false);
	}

	[Fact]
	public async Task DisposeAsync_IsIdempotent()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();

		// Act & Assert - Multiple disposes should not throw
		await Should.NotThrowAsync(async () =>
		{
			await scope.DisposeAsync().ConfigureAwait(false);
			await scope.DisposeAsync().ConfigureAwait(false);
			await scope.DisposeAsync().ConfigureAwait(false);
		}).ConfigureAwait(false);
	}

	[Fact]
	public void ScopeImplementsITransactionScope()
	{
		// Arrange & Act
		using var scope = _provider.CreateTransactionScope();

		// Assert
		_ = scope.ShouldBeAssignableTo<ITransactionScope>();
	}

	[Fact]
	public void ScopeImplementsIDisposable()
	{
		// Arrange & Act
		using var scope = _provider.CreateTransactionScope();

		// Assert
		_ = scope.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void ScopeImplementsIAsyncDisposable()
	{
		// Arrange & Act
		using var scope = _provider.CreateTransactionScope();

		// Assert
		_ = scope.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	/// <inheritdoc/>
	public void Dispose() => _provider?.Dispose();
}
