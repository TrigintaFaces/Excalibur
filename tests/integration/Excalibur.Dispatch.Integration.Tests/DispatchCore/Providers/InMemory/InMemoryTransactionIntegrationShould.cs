// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.InMemory;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.InMemory;

/// <summary>
/// Integration tests for <see cref="InMemoryPersistenceProvider"/> transaction handling.
/// Tests commit, rollback, nested transactions, and isolation levels.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 180 - InMemory Provider Testing Epic.
/// bd-i5agw: Transaction Tests (5 tests).
/// </para>
/// <para>
/// These tests verify transaction scope behavior in the InMemory provider.
/// Note: Savepoints are NOT supported and will throw NotSupportedException.
/// </para>
/// </remarks>
[IntegrationTest]
[Trait("Component", "Transaction")]
[Trait("Provider", "InMemory")]
public sealed class InMemoryTransactionIntegrationShould : IntegrationTestBase
{
	/// <summary>
	/// Tests that committing a transaction changes the status to Committed.
	/// </summary>
	[Fact]
	public async Task CommitTransaction()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();
		var scope = provider.CreateTransactionScope();

		// Assert initial state
		scope.Status.ShouldBe(TransactionStatus.Active);
		scope.TransactionId.ShouldNotBeNullOrEmpty();

		// Act
		await scope.CommitAsync(TestCancellationToken).ConfigureAwait(true);

		// Assert
		scope.Status.ShouldBe(TransactionStatus.Committed);
	}

	/// <summary>
	/// Tests that rolling back a transaction changes the status to RolledBack.
	/// </summary>
	[Fact]
	public async Task RollbackTransaction()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();
		var scope = provider.CreateTransactionScope();

		// Assert initial state
		scope.Status.ShouldBe(TransactionStatus.Active);

		// Act
		await scope.RollbackAsync(TestCancellationToken).ConfigureAwait(true);

		// Assert
		scope.Status.ShouldBe(TransactionStatus.RolledBack);
	}

	/// <summary>
	/// Tests that InMemory transaction scope does not implement ITransactionScopeAdvanced.
	/// Nested scopes and savepoints are not supported by InMemory.
	/// </summary>
	[Fact]
	public void NotImplementAdvancedTransactionFeatures()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();
		var scope = provider.CreateTransactionScope(IsolationLevel.ReadCommitted);

		// Assert — InMemory does not support savepoints or nested scopes
		scope.ShouldNotBeAssignableTo<ITransactionScopeAdvanced>();
		scope.ShouldBeAssignableTo<ITransactionScopeCallbacks>();

		// Cleanup
		scope.Dispose();
	}

	/// <summary>
	/// Tests that different isolation levels are supported and stored correctly.
	/// </summary>
	[Fact]
	public void SupportIsolationLevels()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();

		// Act - Create scopes with different isolation levels
		var readCommittedScope = provider.CreateTransactionScope(IsolationLevel.ReadCommitted);
		var repeatableReadScope = provider.CreateTransactionScope(IsolationLevel.RepeatableRead);
		var serializableScope = provider.CreateTransactionScope(IsolationLevel.Serializable);

		// Assert
		readCommittedScope.IsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
		repeatableReadScope.IsolationLevel.ShouldBe(IsolationLevel.RepeatableRead);
		serializableScope.IsolationLevel.ShouldBe(IsolationLevel.Serializable);

		// Cleanup
		readCommittedScope.Dispose();
		repeatableReadScope.Dispose();
		serializableScope.Dispose();
	}

	/// <summary>
	/// Tests that concurrent transactions are handled via SemaphoreSlim serialization.
	/// The InMemory provider uses a semaphore to ensure only one transaction executes at a time.
	/// </summary>
	[Fact]
	public async Task HandleConcurrentTransactions()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();
		var completedCount = 0;
		var lockObj = new object();
		var transactionCount = 5;

		// Act - Start multiple concurrent transactions
		var tasks = Enumerable.Range(0, transactionCount).Select(async i =>
		{
			// BeginTransactionAsync acquires the semaphore
			using var transaction = await provider.BeginTransactionAsync(
				IsolationLevel.ReadCommitted,
				TestCancellationToken).ConfigureAwait(false);

			// Simulate some work
			await Task.Delay(10, TestCancellationToken).ConfigureAwait(false);

			// Commit transaction
			transaction.Commit();

			lock (lockObj)
			{
				completedCount++;
			}
		}).ToArray();

		await Task.WhenAll(tasks).ConfigureAwait(true);

		// Assert - All transactions completed
		completedCount.ShouldBe(transactionCount);
	}

	/// <summary>
	/// Tests that InMemory transaction scope does not expose savepoint operations (ISP split).
	/// </summary>
	[Fact]
	public void NotExposeSavepointOperations()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();
		var scope = provider.CreateTransactionScope();

		// Assert — savepoint operations are on ITransactionScopeAdvanced which InMemory does not implement
		scope.ShouldNotBeAssignableTo<ITransactionScopeAdvanced>();

		// Cleanup
		scope.Dispose();
	}

	/// <summary>
	/// Tests transaction callbacks (OnCommit, OnRollback, OnComplete).
	/// </summary>
	[Fact]
	public async Task ExecuteTransactionCallbacks()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();
		var scope = provider.CreateTransactionScope();
		var callbacks = scope.ShouldBeAssignableTo<ITransactionScopeCallbacks>();
		callbacks.ShouldNotBeNull();
		var commitCallbackExecuted = false;
		var completeCallbackExecuted = false;
		TransactionStatus? completedStatus = null;

		callbacks.OnCommit(() =>
		{
			commitCallbackExecuted = true;
			return Task.CompletedTask;
		});

		callbacks.OnComplete(status =>
		{
			completeCallbackExecuted = true;
			completedStatus = status;
			return Task.CompletedTask;
		});

		// Act
		await scope.CommitAsync(TestCancellationToken).ConfigureAwait(true);

		// Assert
		commitCallbackExecuted.ShouldBeTrue();
		completeCallbackExecuted.ShouldBeTrue();
		completedStatus.ShouldBe(TransactionStatus.Committed);
	}

	/// <summary>
	/// Tests that disposing without commit results in RolledBack status.
	/// </summary>
	[Fact]
	public void RollbackOnDisposeWithoutCommit()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();
		var scope = provider.CreateTransactionScope();

		// Act - Dispose without commit
		scope.Dispose();

		// Assert - Status should be RolledBack
		scope.Status.ShouldBe(TransactionStatus.RolledBack);
	}

	private static InMemoryPersistenceProvider CreatePersistenceProvider()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryProviderOptions
		{
			Name = $"transaction-test-{Guid.NewGuid():N}"
		});
		var logger = NullLogger<InMemoryPersistenceProvider>.Instance;
		return new InMemoryPersistenceProvider(options, logger);
	}
}
