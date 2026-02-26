// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// Depth tests for <see cref="TransactionScopeBase"/>.
/// Covers callbacks, provider enlistment, status guards, timeout, and disposal.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TransactionScopeBaseDepthShould
{
	[Fact]
	public void InitializeWithCorrectDefaultValues()
	{
		var lowerBound = DateTimeOffset.UtcNow;

		// Act
		var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromSeconds(30));
		var upperBound = DateTimeOffset.UtcNow;

		// Assert
		scope.TransactionId.ShouldNotBeNullOrWhiteSpace();
		scope.IsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
		scope.Status.ShouldBe(TransactionStatus.Active);
		scope.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
		scope.StartTime.ShouldBeGreaterThanOrEqualTo(lowerBound);
		scope.StartTime.ShouldBeLessThanOrEqualTo(upperBound);
	}

	[Fact]
	public void RegisterCommitCallback()
	{
		// Arrange
		var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromMinutes(5));
		var callbackCalled = false;

		// Act
		scope.OnCommit(() => { callbackCalled = true; return Task.CompletedTask; });

		// Assert - callback registered but not called yet
		callbackCalled.ShouldBeFalse();
	}

	[Fact]
	public void ThrowWhenCommitCallbackIsNull()
	{
		// Arrange
		var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromMinutes(5));

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => scope.OnCommit(null!));
	}

	[Fact]
	public void RegisterRollbackCallback()
	{
		// Arrange
		var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromMinutes(5));
		var callbackCalled = false;

		// Act
		scope.OnRollback(() => { callbackCalled = true; return Task.CompletedTask; });

		// Assert
		callbackCalled.ShouldBeFalse();
	}

	[Fact]
	public void ThrowWhenRollbackCallbackIsNull()
	{
		// Arrange
		var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromMinutes(5));

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => scope.OnRollback(null!));
	}

	[Fact]
	public void RegisterCompleteCallback()
	{
		// Arrange
		var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromMinutes(5));
		var callbackCalled = false;

		// Act
		scope.OnComplete(_ => { callbackCalled = true; return Task.CompletedTask; });

		// Assert
		callbackCalled.ShouldBeFalse();
	}

	[Fact]
	public void ThrowWhenCompleteCallbackIsNull()
	{
		// Arrange
		var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromMinutes(5));

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => scope.OnComplete(null!));
	}

	[Fact]
	public async Task ExecuteCommitCallbacksSuccessfully()
	{
		// Arrange
		var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromMinutes(5));
		var called1 = false;
		var called2 = false;
		scope.OnCommit(() => { called1 = true; return Task.CompletedTask; });
		scope.OnCommit(() => { called2 = true; return Task.CompletedTask; });

		// Act
		var errors = await scope.CallExecuteCommitCallbacksAsync();

		// Assert
		errors.ShouldBeNull();
		called1.ShouldBeTrue();
		called2.ShouldBeTrue();
	}

	[Fact]
	public async Task CollectCommitCallbackErrors()
	{
		// Arrange
		var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromMinutes(5));
		scope.OnCommit(() => throw new InvalidOperationException("callback error"));

		// Act
		var errors = await scope.CallExecuteCommitCallbacksAsync();

		// Assert
		errors.ShouldNotBeNull();
		errors!.Count.ShouldBe(1);
		errors[0].ShouldBeOfType<InvalidOperationException>();
	}

	[Fact]
	public async Task ExecuteRollbackCallbacksSuccessfully()
	{
		// Arrange
		var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromMinutes(5));
		var called = false;
		scope.OnRollback(() => { called = true; return Task.CompletedTask; });

		// Act
		var errors = await scope.CallExecuteRollbackCallbacksAsync();

		// Assert
		errors.ShouldBeNull();
		called.ShouldBeTrue();
	}

	[Fact]
	public async Task CollectRollbackCallbackErrors()
	{
		// Arrange
		var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromMinutes(5));
		scope.OnRollback(() => throw new InvalidOperationException("rollback error"));

		// Act
		var errors = await scope.CallExecuteRollbackCallbacksAsync();

		// Assert
		errors.ShouldNotBeNull();
		errors!.Count.ShouldBe(1);
	}

	[Fact]
	public async Task ExecuteCompleteCallbacksWithStatus()
	{
		// Arrange
		var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromMinutes(5));
		TransactionStatus? receivedStatus = null;
		scope.OnComplete(status => { receivedStatus = status; return Task.CompletedTask; });

		// Act
		var errors = await scope.CallExecuteCompleteCallbacksAsync(null);

		// Assert
		errors.ShouldBeNull();
		receivedStatus.ShouldBe(TransactionStatus.Active);
	}

	[Fact]
	public void EnlistProviderSuccessfully()
	{
		// Arrange
		var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromMinutes(5));
		var provider = A.Fake<IPersistenceProvider>();

		// Act
		var enlisted = scope.CallTryEnlistProvider(provider);

		// Assert
		enlisted.ShouldBeTrue();
		scope.GetEnlistedProviders().ShouldContain(provider);
	}

	[Fact]
	public void NotEnlistDuplicateProvider()
	{
		// Arrange
		var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromMinutes(5));
		var provider = A.Fake<IPersistenceProvider>();

		// Act
		var enlisted1 = scope.CallTryEnlistProvider(provider);
		var enlisted2 = scope.CallTryEnlistProvider(provider);

		// Assert
		enlisted1.ShouldBeTrue();
		enlisted2.ShouldBeFalse();
		scope.GetEnlistedProviders().Count().ShouldBe(1);
	}

	[Fact]
	public void ReturnEmptyEnlistedProviders()
	{
		// Arrange
		var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromMinutes(5));

		// Act
		var providers = scope.GetEnlistedProviders();

		// Assert
		providers.ShouldBeEmpty();
	}

	[Fact]
	public void ThrowIfNotActiveWhenStatusIsCommitted()
	{
		// Arrange
		var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromMinutes(5));
		scope.SetStatus(TransactionStatus.Committed);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => scope.CallThrowIfNotActive());
	}

	[Fact]
	public void ThrowTimeoutExceptionWhenTransactionTimedOut()
	{
		// Arrange - set a very short timeout
		var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.Zero);

		// Act & Assert
		Should.Throw<TimeoutException>(() => scope.CallThrowIfNotActive());
		scope.Status.ShouldBe(TransactionStatus.TimedOut);
	}

	[Fact]
	public void ThrowIfDisposedWhenDisposed()
	{
		// Arrange
		var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromMinutes(5));
		scope.Dispose();

		// Act & Assert
		Should.Throw<ObjectDisposedException>(() => scope.OnCommit(() => Task.CompletedTask));
	}

	[Fact]
	public void ThrowAggregateExceptionWhenCallbackErrorsExist()
	{
		// Arrange
		var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromMinutes(5));
		var errors = new List<Exception> { new InvalidOperationException("error1") };

		// Act & Assert
		Should.Throw<AggregateException>(() => scope.CallThrowIfCallbackErrors(errors));
	}

	[Fact]
	public void NotThrowWhenCallbackErrorsIsNull()
	{
		// Arrange
		var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromMinutes(5));

		// Act & Assert - should not throw
		scope.CallThrowIfCallbackErrors(null);
	}

	[Fact]
	public void NotThrowWhenCallbackErrorsIsEmpty()
	{
		// Arrange
		var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromMinutes(5));

		// Act & Assert - should not throw
		scope.CallThrowIfCallbackErrors([]);
	}

	[Fact]
	public void AllowSettingTimeout()
	{
		// Arrange
		var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromMinutes(5));

		// Act
		scope.Timeout = TimeSpan.FromSeconds(10);

		// Assert
		scope.Timeout.ShouldBe(TimeSpan.FromSeconds(10));
	}

	/// <summary>
	/// Concrete test implementation of TransactionScopeBase.
	/// </summary>
	private sealed class TestTransactionScope : TransactionScopeBase
	{
		public TestTransactionScope(IsolationLevel isolationLevel, TimeSpan timeout)
			: base(isolationLevel, timeout) { }

		public override Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
		public override Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;
		public override Task EnlistProviderAsync(IPersistenceProvider provider, CancellationToken cancellationToken) => Task.CompletedTask;
		public override Task EnlistConnectionAsync(IDbConnection connection, CancellationToken cancellationToken) => Task.CompletedTask;
		public override ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
		protected override void Dispose(bool disposing) { Disposed = true; }

		public void SetStatus(TransactionStatus status) => Status = status;
		public bool CallTryEnlistProvider(IPersistenceProvider provider) => TryEnlistProvider(provider);
		public Task<List<Exception>?> CallExecuteCommitCallbacksAsync() => ExecuteCommitCallbacksAsync();
		public Task<List<Exception>?> CallExecuteRollbackCallbacksAsync() => ExecuteRollbackCallbacksAsync();
		public Task<List<Exception>?> CallExecuteCompleteCallbacksAsync(List<Exception>? errors) => ExecuteCompleteCallbacksAsync(errors);
		public void CallThrowIfNotActive() => ThrowIfNotActive();
		public void CallThrowIfCallbackErrors(List<Exception>? errors) => ThrowIfCallbackErrors(errors);
	}
}
