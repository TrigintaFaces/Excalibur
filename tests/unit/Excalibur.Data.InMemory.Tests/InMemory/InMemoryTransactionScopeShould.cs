// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.InMemory;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Unit tests for the ITransactionScope implementation returned by InMemoryPersistenceProvider.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.InMemory")]
public sealed class InMemoryTransactionScopeShould : UnitTestBase
{
	private readonly ILogger<InMemoryPersistenceProvider> _logger;
	private readonly InMemoryPersistenceProvider _provider;

	public InMemoryTransactionScopeShould()
	{
		_logger = A.Fake<ILogger<InMemoryPersistenceProvider>>();
		var options = Options.Create(new InMemoryProviderOptions { Name = "TestProvider" });
		_provider = new InMemoryPersistenceProvider(options, _logger);
	}

	#region Constructor and Properties

	[Fact]
	public void HaveActiveStatusWhenCreated()
	{
		// Act
		var scope = _provider.CreateTransactionScope();

		// Assert
		scope.Status.ShouldBe(TransactionStatus.Active);
	}

	[Fact]
	public void HaveUniqueTransactionId()
	{
		// Act
		var scope1 = _provider.CreateTransactionScope();
		var scope2 = _provider.CreateTransactionScope();

		// Assert
		scope1.TransactionId.ShouldNotBeNullOrEmpty();
		scope2.TransactionId.ShouldNotBeNullOrEmpty();
		scope1.TransactionId.ShouldNotBe(scope2.TransactionId);
	}

	[Fact]
	public void HaveDefaultIsolationLevelOfReadCommitted()
	{
		// Act
		var scope = _provider.CreateTransactionScope();

		// Assert
		scope.IsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
	}

	[Fact]
	public void HaveCorrectIsolationLevelWhenSpecified()
	{
		// Act
		var scope = _provider.CreateTransactionScope(IsolationLevel.Serializable);

		// Assert
		scope.IsolationLevel.ShouldBe(IsolationLevel.Serializable);
	}

	[Fact]
	public void HaveStartTimeSetOnCreation()
	{
		// Arrange
		var beforeCreate = DateTime.UtcNow;

		// Act
		var scope = _provider.CreateTransactionScope();

		// Assert
		var afterCreate = DateTime.UtcNow;
		scope.StartTime.ShouldBeGreaterThanOrEqualTo(beforeCreate);
		scope.StartTime.ShouldBeLessThanOrEqualTo(afterCreate);
	}

	[Fact]
	public void HaveDefaultTimeoutOfOneMinute()
	{
		// Act
		var scope = _provider.CreateTransactionScope();

		// Assert
		scope.Timeout.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void HaveCustomTimeoutWhenSpecified()
	{
		// Act
		var scope = _provider.CreateTransactionScope(timeout: TimeSpan.FromSeconds(30));

		// Assert
		scope.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void AllowTimeoutToBeModified()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();

		// Act
		scope.Timeout = TimeSpan.FromMinutes(5);

		// Assert
		scope.Timeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	#endregion Constructor and Properties

	#region CommitAsync Tests

	[Fact]
	public async Task CommitAsync_SetsStatusToCommitted()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();

		// Act
		await scope.CommitAsync(CancellationToken.None);

		// Assert
		scope.Status.ShouldBe(TransactionStatus.Committed);
	}

	[Fact]
	public async Task CommitAsync_ExecutesOnCommitCallbacks()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		var callbackExecuted = false;
		GetCallbacks(scope).OnCommit(() =>
		{
			callbackExecuted = true;
			return Task.CompletedTask;
		});

		// Act
		await scope.CommitAsync(CancellationToken.None);

		// Assert
		callbackExecuted.ShouldBeTrue();
	}

	[Fact]
	public async Task CommitAsync_ExecutesMultipleOnCommitCallbacksInOrder()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		var executionOrder = new List<int>();
		GetCallbacks(scope).OnCommit(() =>
		{
			executionOrder.Add(1);
			return Task.CompletedTask;
		});
		GetCallbacks(scope).OnCommit(() =>
		{
			executionOrder.Add(2);
			return Task.CompletedTask;
		});
		GetCallbacks(scope).OnCommit(() =>
		{
			executionOrder.Add(3);
			return Task.CompletedTask;
		});

		// Act
		await scope.CommitAsync(CancellationToken.None);

		// Assert
		executionOrder.ShouldBe([1, 2, 3]);
	}

	[Fact]
	public async Task CommitAsync_ExecutesOnCompleteCallbacksWithCommittedStatus()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		TransactionStatus? receivedStatus = null;
		GetCallbacks(scope).OnComplete(status =>
		{
			receivedStatus = status;
			return Task.CompletedTask;
		});

		// Act
		await scope.CommitAsync(CancellationToken.None);

		// Assert
		receivedStatus.ShouldBe(TransactionStatus.Committed);
	}

	[Fact]
	public async Task CommitAsync_ThrowsInvalidOperationException_WhenAlreadyCommitted()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		await scope.CommitAsync(CancellationToken.None);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
			scope.CommitAsync(CancellationToken.None));
		exception.Message.ShouldContain("Committed");
	}

	[Fact]
	public async Task CommitAsync_ThrowsInvalidOperationException_WhenRolledBack()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		await scope.RollbackAsync(CancellationToken.None);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
			scope.CommitAsync(CancellationToken.None));
		exception.Message.ShouldContain("RolledBack");
	}

	[Fact]
	public async Task CommitAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		scope.Dispose();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(() =>
			scope.CommitAsync(CancellationToken.None));
	}

	[Fact]
	public async Task CommitAsync_SetsStatusToRolledBack_WhenCallbackThrows()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		GetCallbacks(scope).OnCommit(() => throw new InvalidOperationException("Callback failed"));

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(() =>
			scope.CommitAsync(CancellationToken.None));
		scope.Status.ShouldBe(TransactionStatus.RolledBack);
	}

	#endregion CommitAsync Tests

	#region RollbackAsync Tests

	[Fact]
	public async Task RollbackAsync_SetsStatusToRolledBack()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();

		// Act
		await scope.RollbackAsync(CancellationToken.None);

		// Assert
		scope.Status.ShouldBe(TransactionStatus.RolledBack);
	}

	[Fact]
	public async Task RollbackAsync_ExecutesOnRollbackCallbacks()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		var callbackExecuted = false;
		GetCallbacks(scope).OnRollback(() =>
		{
			callbackExecuted = true;
			return Task.CompletedTask;
		});

		// Act
		await scope.RollbackAsync(CancellationToken.None);

		// Assert
		callbackExecuted.ShouldBeTrue();
	}

	[Fact]
	public async Task RollbackAsync_ExecutesOnCompleteCallbacksWithRolledBackStatus()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		TransactionStatus? receivedStatus = null;
		GetCallbacks(scope).OnComplete(status =>
		{
			receivedStatus = status;
			return Task.CompletedTask;
		});

		// Act
		await scope.RollbackAsync(CancellationToken.None);

		// Assert
		receivedStatus.ShouldBe(TransactionStatus.RolledBack);
	}

	[Fact]
	public async Task RollbackAsync_ThrowsInvalidOperationException_WhenAlreadyCommitted()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		await scope.CommitAsync(CancellationToken.None);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
			scope.RollbackAsync(CancellationToken.None));
		exception.Message.ShouldContain("Committed");
	}

	[Fact]
	public async Task RollbackAsync_ThrowsInvalidOperationException_WhenAlreadyRolledBack()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		await scope.RollbackAsync(CancellationToken.None);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
			scope.RollbackAsync(CancellationToken.None));
		exception.Message.ShouldContain("RolledBack");
	}

	[Fact]
	public async Task RollbackAsync_ThrowsObjectDisposedException_WhenDisposed()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		scope.Dispose();

		// Act & Assert
		_ = await Should.ThrowAsync<ObjectDisposedException>(() =>
			scope.RollbackAsync(CancellationToken.None));
	}

	[Fact]
	public async Task RollbackAsync_SetsStatusToRolledBack_EvenWhenCallbackThrows()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		GetCallbacks(scope).OnRollback(() => throw new InvalidOperationException("Callback failed"));

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(() =>
			scope.RollbackAsync(CancellationToken.None));
		scope.Status.ShouldBe(TransactionStatus.RolledBack);
	}

	#endregion RollbackAsync Tests

	#region Callback Registration Tests

	[Fact]
	public void OnCommit_ThrowsArgumentNullException_WhenCallbackIsNull()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			GetCallbacks(scope).OnCommit(null!));
	}

	[Fact]
	public void OnRollback_ThrowsArgumentNullException_WhenCallbackIsNull()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			GetCallbacks(scope).OnRollback(null!));
	}

	[Fact]
	public void OnComplete_ThrowsArgumentNullException_WhenCallbackIsNull()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			GetCallbacks(scope).OnComplete(null!));
	}

	#endregion Callback Registration Tests

	#region EnlistProviderAsync Tests

	[Fact]
	public async Task EnlistProviderAsync_ThrowsArgumentNullException_WhenProviderIsNull()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			scope.EnlistProviderAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task EnlistProviderAsync_SucceedsForCreatingProvider()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();

		// Act & Assert - Should not throw
		await Should.NotThrowAsync(() =>
			scope.EnlistProviderAsync(_provider, CancellationToken.None));
	}

	[Fact]
	public async Task EnlistProviderAsync_ThrowsNotSupportedException_ForDifferentProvider()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		var otherOptions = Options.Create(new InMemoryProviderOptions { Name = "OtherProvider" });
		var otherProvider = new InMemoryPersistenceProvider(otherOptions, _logger);

		// Act & Assert
		var exception = await Should.ThrowAsync<NotSupportedException>(() =>
			scope.EnlistProviderAsync(otherProvider, CancellationToken.None));
		exception.Message.ShouldContain("only supports creating provider");

		// Cleanup
		otherProvider.Dispose();
	}

	#endregion EnlistProviderAsync Tests

	#region EnlistConnectionAsync Tests

	[Fact]
	public async Task EnlistConnectionAsync_CompletesSuccessfully()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		var connection = _provider.CreateConnection();

		// Act & Assert - Should not throw
		await Should.NotThrowAsync(() =>
			scope.EnlistConnectionAsync(connection, CancellationToken.None));

		connection.Dispose();
	}

	#endregion EnlistConnectionAsync Tests

	#region ISP Sub-Interface Tests

	[Fact]
	public void DoesNotImplementITransactionScopeAdvanced()
	{
		// Arrange — InMemory does not support savepoints or nested scopes
		var scope = _provider.CreateTransactionScope();

		// Assert
		scope.ShouldNotBeAssignableTo<ITransactionScopeAdvanced>();
	}

	[Fact]
	public void ImplementsITransactionScopeCallbacks()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();

		// Assert
		_ = scope.ShouldBeAssignableTo<ITransactionScopeCallbacks>();
	}

	#endregion ISP Sub-Interface Tests

	#region GetEnlistedProviders Tests

	[Fact]
	public void GetEnlistedProviders_ReturnsCreatingProviderByDefault()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();

		// Act
		var providers = scope.GetEnlistedProviders().ToList();

		// Assert
		providers.ShouldHaveSingleItem();
		providers[0].ShouldBeSameAs(_provider);
	}

	#endregion GetEnlistedProviders Tests

	// CreateNestedScope tests removed — InMemory does not implement ITransactionScopeAdvanced (ISP split S558.22)

	#region Dispose Tests

	[Fact]
	public void Dispose_SetsStatusToRolledBack_WhenActive()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		scope.Status.ShouldBe(TransactionStatus.Active);

		// Act
		scope.Dispose();

		// Assert
		scope.Status.ShouldBe(TransactionStatus.RolledBack);
	}

	[Fact]
	public async Task Dispose_DoesNotChangeStatus_WhenAlreadyCommitted()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		await scope.CommitAsync(CancellationToken.None);

		// Act
		scope.Dispose();

		// Assert
		scope.Status.ShouldBe(TransactionStatus.Committed);
	}

	[Fact]
	public async Task Dispose_DoesNotChangeStatus_WhenAlreadyRolledBack()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		await scope.RollbackAsync(CancellationToken.None);

		// Act
		scope.Dispose();

		// Assert
		scope.Status.ShouldBe(TransactionStatus.RolledBack);
	}

	[Fact]
	public void Dispose_IsIdempotent()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			scope.Dispose();
			scope.Dispose();
			scope.Dispose();
		});
	}

	[Fact]
	public async Task DisposeAsync_SetsStatusToRolledBack_WhenActive()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();
		scope.Status.ShouldBe(TransactionStatus.Active);

		// Act
		await scope.DisposeAsync();

		// Assert
		scope.Status.ShouldBe(TransactionStatus.RolledBack);
	}

	[Fact]
	public async Task DisposeAsync_IsIdempotent()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();

		// Act & Assert - Should not throw
		await Should.NotThrowAsync(async () =>
		{
			await scope.DisposeAsync();
			await scope.DisposeAsync();
			await scope.DisposeAsync();
		});
	}

	#endregion Dispose Tests

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsITransactionScope()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();

		// Assert
		_ = scope.ShouldBeAssignableTo<ITransactionScope>();
	}

	[Fact]
	public void ImplementsIDisposable()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();

		// Assert
		_ = scope.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void ImplementsIAsyncDisposable()
	{
		// Arrange
		var scope = _provider.CreateTransactionScope();

		// Assert
		_ = scope.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	#endregion Interface Implementation Tests

	/// <summary>
	/// Helper to get the ITransactionScopeCallbacks sub-interface from a transaction scope.
	/// </summary>
	private static ITransactionScopeCallbacks GetCallbacks(ITransactionScope scope) =>
		(ITransactionScopeCallbacks)scope;

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
