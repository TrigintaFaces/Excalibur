// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.InMemory;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Depth tests for InMemoryTransactionScope (accessed via InMemoryPersistenceProvider.CreateTransactionScope).
/// Covers commit, rollback, callbacks, disposal, and provider enlistment.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryTransactionScopeDepthShould
{
	private InMemoryPersistenceProvider CreateProvider()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryProviderOptions { Name = "test" });
		return new InMemoryPersistenceProvider(options, NullLogger<InMemoryPersistenceProvider>.Instance);
	}

	[Fact]
	public void HaveActiveStatusOnCreation()
	{
		// Arrange
		using var provider = CreateProvider();
		using var scope = provider.CreateTransactionScope();

		// Assert
		scope.Status.ShouldBe(TransactionStatus.Active);
		scope.TransactionId.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void HaveDefaultIsolationLevel()
	{
		// Arrange
		using var provider = CreateProvider();
		using var scope = provider.CreateTransactionScope();

		// Assert
		scope.IsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
	}

	[Fact]
	public void HaveCustomIsolationLevel()
	{
		// Arrange
		using var provider = CreateProvider();
		using var scope = provider.CreateTransactionScope(IsolationLevel.Serializable);

		// Assert
		scope.IsolationLevel.ShouldBe(IsolationLevel.Serializable);
	}

	[Fact]
	public async Task CommitSuccessfully()
	{
		// Arrange
		using var provider = CreateProvider();
		var scope = provider.CreateTransactionScope();

		// Act
		await scope.CommitAsync(CancellationToken.None);

		// Assert
		scope.Status.ShouldBe(TransactionStatus.Committed);
		await scope.DisposeAsync();
	}

	[Fact]
	public async Task RollbackSuccessfully()
	{
		// Arrange
		using var provider = CreateProvider();
		var scope = provider.CreateTransactionScope();

		// Act
		await scope.RollbackAsync(CancellationToken.None);

		// Assert
		scope.Status.ShouldBe(TransactionStatus.RolledBack);
		await scope.DisposeAsync();
	}

	[Fact]
	public async Task ExecuteCommitCallbacks()
	{
		// Arrange
		using var provider = CreateProvider();
		var scope = provider.CreateTransactionScope();
		var callbackCalled = false;

		// The scope implements ITransactionScopeCallbacks
		var callbacks = scope as ITransactionScopeCallbacks;
		callbacks.ShouldNotBeNull();
		callbacks!.OnCommit(() => { callbackCalled = true; return Task.CompletedTask; });

		// Act
		await scope.CommitAsync(CancellationToken.None);

		// Assert
		callbackCalled.ShouldBeTrue();
		await scope.DisposeAsync();
	}

	[Fact]
	public async Task ExecuteRollbackCallbacks()
	{
		// Arrange
		using var provider = CreateProvider();
		var scope = provider.CreateTransactionScope();
		var callbackCalled = false;

		var callbacks = scope as ITransactionScopeCallbacks;
		callbacks.ShouldNotBeNull();
		callbacks!.OnRollback(() => { callbackCalled = true; return Task.CompletedTask; });

		// Act
		await scope.RollbackAsync(CancellationToken.None);

		// Assert
		callbackCalled.ShouldBeTrue();
		await scope.DisposeAsync();
	}

	[Fact]
	public async Task ExecuteCompleteCallbacksOnCommit()
	{
		// Arrange
		using var provider = CreateProvider();
		var scope = provider.CreateTransactionScope();
		TransactionStatus? receivedStatus = null;

		var callbacks = scope as ITransactionScopeCallbacks;
		callbacks.ShouldNotBeNull();
		callbacks!.OnComplete(status => { receivedStatus = status; return Task.CompletedTask; });

		// Act
		await scope.CommitAsync(CancellationToken.None);

		// Assert
		receivedStatus.ShouldBe(TransactionStatus.Committed);
		await scope.DisposeAsync();
	}

	[Fact]
	public async Task EnlistProvider()
	{
		// Arrange
		using var provider = CreateProvider();
		var scope = provider.CreateTransactionScope();

		// Act
		await scope.EnlistProviderAsync(provider, CancellationToken.None);

		// Assert
		scope.GetEnlistedProviders().ShouldContain(provider);
		await scope.DisposeAsync();
	}

	[Fact]
	public async Task EnlistConnection()
	{
		// Arrange
		using var provider = CreateProvider();
		var scope = provider.CreateTransactionScope();
		using var connection = provider.CreateConnection();

		// Act & Assert - should not throw
		await scope.EnlistConnectionAsync(connection, CancellationToken.None);
		await scope.DisposeAsync();
	}

	[Fact]
	public async Task ThrowWhenCommittingNonActiveScope()
	{
		// Arrange
		using var provider = CreateProvider();
		var scope = provider.CreateTransactionScope();
		await scope.CommitAsync(CancellationToken.None);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() =>
			scope.CommitAsync(CancellationToken.None));
		await scope.DisposeAsync();
	}

	[Fact]
	public async Task ThrowWhenRollingBackNonActiveScope()
	{
		// Arrange
		using var provider = CreateProvider();
		var scope = provider.CreateTransactionScope();
		await scope.CommitAsync(CancellationToken.None);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() =>
			scope.RollbackAsync(CancellationToken.None));
		await scope.DisposeAsync();
	}

	[Fact]
	public async Task DisposeAsyncSafely()
	{
		// Arrange
		using var provider = CreateProvider();
		var scope = provider.CreateTransactionScope();

		// Act & Assert - should not throw
		await scope.DisposeAsync();
	}

	[Fact]
	public void DisposeSyncSafely()
	{
		// Arrange
		using var provider = CreateProvider();
		var scope = provider.CreateTransactionScope();

		// Act & Assert - should not throw
		scope.Dispose();
	}
}
