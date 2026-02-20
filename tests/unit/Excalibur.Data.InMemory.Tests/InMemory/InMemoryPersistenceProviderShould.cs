// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Persistence;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Excalibur.Data.InMemory;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Unit tests for InMemoryPersistenceProvider operations.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InMemoryPersistenceProviderShould : UnitTestBase
{
	private readonly ILogger<InMemoryPersistenceProvider> _logger;
	private readonly InMemoryPersistenceProvider _provider;

	public InMemoryPersistenceProviderShould()
	{
		_logger = A.Fake<ILogger<InMemoryPersistenceProvider>>();
		var options = Options.Create(new InMemoryProviderOptions { Name = "TestProvider" });
		_provider = new InMemoryPersistenceProvider(options, _logger);
	}

	#region Constructor and Properties

	[Fact]
	public void InitializeWithCorrectProperties()
	{
		// Assert
		_ = _provider.ShouldNotBeNull();
		_provider.Name.ShouldBe("TestProvider");
		_provider.ProviderType.ShouldBe("InMemory");
		_provider.IsAvailable.ShouldBeTrue();
		_provider.IsReadOnly.ShouldBeFalse();
		_provider.ConnectionString.ShouldContain("InMemory");
	}

	[Fact]
	public void HaveNullRetryPolicy()
	{
		// Assert
		_ = _provider.RetryPolicy.ShouldNotBeNull();
		_provider.RetryPolicy.MaxRetryAttempts.ShouldBe(0);
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new InMemoryPersistenceProvider(null!, _logger));
	}

	[Fact]
	public void ThrowOnNullLogger()
	{
		// Arrange
		var options = Options.Create(new InMemoryProviderOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new InMemoryPersistenceProvider(options, null!));
	}

	[Fact]
	public void UseDefaultNameWhenNotProvided()
	{
		// Arrange
		var options = Options.Create(new InMemoryProviderOptions());
		var provider = new InMemoryPersistenceProvider(options, _logger);

		// Assert
		provider.Name.ShouldBe("inmemory");
		provider.Dispose();
	}

	#endregion Constructor and Properties

	#region Connection Creation

	[Fact]
	public void CreateConnection()
	{
		// Act
		var connection = _provider.CreateConnection();

		// Assert
		_ = connection.ShouldNotBeNull();
		// InMemory connections start in Closed state and can be opened
		connection.State.ShouldBe(ConnectionState.Closed);
		connection.Dispose();
	}

	[Fact]
	public async Task CreateConnectionAsync()
	{
		// Act
		var connection = await _provider.CreateConnectionAsync(CancellationToken.None);

		// Assert
		_ = connection.ShouldNotBeNull();
		// InMemory connections start in Closed state and can be opened
		connection.State.ShouldBe(ConnectionState.Closed);
		connection.Dispose();
	}

	#endregion Connection Creation

	#region Transaction Support

	[Fact]
	public void BeginTransaction()
	{
		// Act
		var transaction = _provider.BeginTransaction();

		// Assert
		_ = transaction.ShouldNotBeNull();
		transaction.IsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
		transaction.Dispose();
	}

	[Fact]
	public void BeginTransactionWithIsolationLevel()
	{
		// Act
		var transaction = _provider.BeginTransaction(IsolationLevel.Serializable);

		// Assert
		_ = transaction.ShouldNotBeNull();
		transaction.IsolationLevel.ShouldBe(IsolationLevel.Serializable);
		transaction.Dispose();
	}

	[Fact]
	public async Task BeginTransactionAsync()
	{
		// Act
		var transaction = await _provider.BeginTransactionAsync(IsolationLevel.ReadCommitted, CancellationToken.None);

		// Assert
		_ = transaction.ShouldNotBeNull();
		transaction.IsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
		transaction.Dispose();
	}

	[Fact]
	public void CreateTransactionScope()
	{
		// Act
		var scope = _provider.CreateTransactionScope();

		// Assert
		_ = scope.ShouldNotBeNull();
		_ = scope.ShouldBeAssignableTo<ITransactionScope>();
	}

	[Fact]
	public void CreateTransactionScopeWithIsolationLevel()
	{
		// Act
		var scope = _provider.CreateTransactionScope(IsolationLevel.Serializable);

		// Assert
		_ = scope.ShouldNotBeNull();
	}

	#endregion Transaction Support

	#region Test Connection

	[Fact]
	public async Task TestConnectionAsync_ReturnsTrue()
	{
		// Act
		var result = await _provider.TestConnectionAsync(CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	#endregion Test Connection

	#region Metrics

	[Fact]
	public async Task GetMetricsAsync_ReturnsMetrics()
	{
		// Act
		var metrics = await _provider.GetMetricsAsync(CancellationToken.None);

		// Assert
		_ = metrics.ShouldNotBeNull();
		metrics.ShouldContainKey("Provider");
		metrics["Provider"].ShouldBe("InMemory");
		metrics.ShouldContainKey("Name");
		metrics["Name"].ShouldBe("TestProvider");
		metrics.ShouldContainKey("IsAvailable");
		metrics.ShouldContainKey("Collections");
		metrics.ShouldContainKey("TotalItems");
	}

	#endregion Metrics

	#region Execute Methods

	[Fact]
	public async Task ExecuteAsync_ThrowsArgumentNullException_WhenRequestIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_provider.ExecuteAsync<IDbConnection, string>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteInTransactionAsync_ThrowsArgumentNullException_WhenRequestIsNull()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_provider.ExecuteInTransactionAsync<IDbConnection, string>(null!, scope, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteInTransactionAsync_ThrowsArgumentNullException_WhenScopeIsNull()
	{
		// Arrange
		var request = A.Fake<IDataRequest<IDbConnection, string>>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_provider.ExecuteInTransactionAsync(request, null!, CancellationToken.None));
	}

	#endregion Execute Methods

	#region Connection Pool Stats

	[Fact]
	public async Task GetConnectionPoolStatsAsync_ReturnsNull()
	{
		// Act - InMemory provider doesn't have connection pooling
		var stats = await _provider.GetConnectionPoolStatsAsync(CancellationToken.None);

		// Assert
		stats.ShouldBeNull();
	}

	#endregion Connection Pool Stats

	#region Disposal

	[Fact]
	public void DisposeDoesNotThrow()
	{
		// Act & Assert
		Should.NotThrow(_provider.Dispose);
	}

	[Fact]
	public async Task DisposeAsyncDoesNotThrow()
	{
		// Act & Assert
		await Should.NotThrowAsync(() => _provider.DisposeAsync().AsTask());
	}

	[Fact]
	public void ThrowsObjectDisposedExceptionAfterDispose()
	{
		// Arrange
		var options = Options.Create(new InMemoryProviderOptions());
		var provider = new InMemoryPersistenceProvider(options, _logger);
		provider.Dispose();

		// Act & Assert
		_ = Should.Throw<ObjectDisposedException>(() => provider.CreateConnection());
	}

	[Fact]
	public async Task ThrowsObjectDisposedExceptionAfterDisposeAsync()
	{
		// Arrange
		var options = Options.Create(new InMemoryProviderOptions());
		var provider = new InMemoryPersistenceProvider(options, _logger);
		await provider.DisposeAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(() =>
			provider.CreateConnectionAsync(CancellationToken.None).AsTask());
	}

	[Fact]
	public void IsAvailableReturnsFalseAfterDispose()
	{
		// Arrange
		var options = Options.Create(new InMemoryProviderOptions());
		var provider = new InMemoryPersistenceProvider(options, _logger);

		// Act
		provider.Dispose();

		// Assert
		provider.IsAvailable.ShouldBeFalse();
	}

	#endregion Disposal

	#region Interface Implementation

	[Fact]
	public void ImplementsIPersistenceProvider()
	{
		// Assert
		_ = _provider.ShouldBeAssignableTo<IPersistenceProvider>();
	}

	[Fact]
	public void ImplementsIDisposable()
	{
		// Assert
		_ = _provider.ShouldBeAssignableTo<IDisposable>();
		_ = _provider.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	#endregion Interface Implementation

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_provider?.Dispose();
		}
		base.Dispose(disposing);
	}
}
