// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.Tests.Abstractions;

// ── Concrete test implementation of TransactionScopeBase ──

public class TestTransactionScope : TransactionScopeBase
{
    public bool CommitCalled { get; private set; }
    public bool RollbackCalled { get; private set; }
    public bool DisposedFlag { get; private set; }
    public bool EnlistProviderCalled { get; private set; }

    public TestTransactionScope(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        TimeSpan? timeout = null)
        : base(isolationLevel, timeout ?? TimeSpan.FromSeconds(30))
    {
    }

    public override async Task CommitAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ThrowIfNotActive();

        CommitCalled = true;
        Status = TransactionStatus.Committed;

        var errors = await ExecuteCommitCallbacksAsync().ConfigureAwait(false);
        errors = await ExecuteCompleteCallbacksAsync(errors).ConfigureAwait(false);
        ThrowIfCallbackErrors(errors);
    }

    public override async Task RollbackAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ThrowIfNotActive();

        RollbackCalled = true;
        Status = TransactionStatus.RolledBack;

        var errors = await ExecuteRollbackCallbacksAsync().ConfigureAwait(false);
        errors = await ExecuteCompleteCallbacksAsync(errors).ConfigureAwait(false);
        ThrowIfCallbackErrors(errors);
    }

    public override Task EnlistProviderAsync(IPersistenceProvider provider, CancellationToken cancellationToken)
    {
        EnlistProviderCalled = true;
        TryEnlistProvider(provider);
        return Task.CompletedTask;
    }

    public override Task EnlistConnectionAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public override ValueTask DisposeAsync()
    {
        Disposed = true;
        DisposedFlag = true;
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Disposed = true;
            DisposedFlag = true;
        }
    }
}

[Trait("Category", "Unit")]
public class TransactionScopeBaseFunctionalShould
{
    [Fact]
    public void Constructor_ShouldInitializeState()
    {
        var scope = new TestTransactionScope();

        scope.TransactionId.ShouldNotBeNullOrWhiteSpace();
        scope.IsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
        scope.Status.ShouldBe(TransactionStatus.Active);
        scope.StartTime.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
        scope.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task CommitAsync_ShouldSetStatusToCommitted()
    {
        var scope = new TestTransactionScope();

        await scope.CommitAsync(CancellationToken.None).ConfigureAwait(false);

        scope.CommitCalled.ShouldBeTrue();
        scope.Status.ShouldBe(TransactionStatus.Committed);
    }

    [Fact]
    public async Task RollbackAsync_ShouldSetStatusToRolledBack()
    {
        var scope = new TestTransactionScope();

        await scope.RollbackAsync(CancellationToken.None).ConfigureAwait(false);

        scope.RollbackCalled.ShouldBeTrue();
        scope.Status.ShouldBe(TransactionStatus.RolledBack);
    }

    [Fact]
    public async Task CommitAsync_ShouldExecuteOnCommitCallbacks()
    {
        var scope = new TestTransactionScope();
        var callbackExecuted = false;

        scope.OnCommit(() =>
        {
            callbackExecuted = true;
            return Task.CompletedTask;
        });

        await scope.CommitAsync(CancellationToken.None).ConfigureAwait(false);

        callbackExecuted.ShouldBeTrue();
    }

    [Fact]
    public async Task RollbackAsync_ShouldExecuteOnRollbackCallbacks()
    {
        var scope = new TestTransactionScope();
        var callbackExecuted = false;

        scope.OnRollback(() =>
        {
            callbackExecuted = true;
            return Task.CompletedTask;
        });

        await scope.RollbackAsync(CancellationToken.None).ConfigureAwait(false);

        callbackExecuted.ShouldBeTrue();
    }

    [Fact]
    public async Task CommitAsync_ShouldExecuteOnCompleteCallbacks()
    {
        var scope = new TestTransactionScope();
        TransactionStatus? completedWith = null;

        scope.OnComplete(status =>
        {
            completedWith = status;
            return Task.CompletedTask;
        });

        await scope.CommitAsync(CancellationToken.None).ConfigureAwait(false);

        completedWith.ShouldBe(TransactionStatus.Committed);
    }

    [Fact]
    public async Task RollbackAsync_ShouldExecuteOnCompleteCallbacksWithRolledBackStatus()
    {
        var scope = new TestTransactionScope();
        TransactionStatus? completedWith = null;

        scope.OnComplete(status =>
        {
            completedWith = status;
            return Task.CompletedTask;
        });

        await scope.RollbackAsync(CancellationToken.None).ConfigureAwait(false);

        completedWith.ShouldBe(TransactionStatus.RolledBack);
    }

    [Fact]
    public async Task CommitAsync_WithFailingCallback_ShouldThrowAggregateException()
    {
        var scope = new TestTransactionScope();

        scope.OnCommit(() => throw new InvalidOperationException("Callback failed"));

        await Should.ThrowAsync<AggregateException>(
            () => scope.CommitAsync(CancellationToken.None)).ConfigureAwait(false);
    }

    [Fact]
    public async Task EnlistProviderAsync_ShouldTrackProviders()
    {
        var scope = new TestTransactionScope();
        var provider = A.Fake<IPersistenceProvider>();

        await scope.EnlistProviderAsync(provider, CancellationToken.None).ConfigureAwait(false);

        var providers = scope.GetEnlistedProviders().ToList();
        providers.Count.ShouldBe(1);
        providers[0].ShouldBe(provider);
    }

    [Fact]
    public async Task EnlistProviderAsync_SameProviderTwice_ShouldNotDuplicate()
    {
        var scope = new TestTransactionScope();
        var provider = A.Fake<IPersistenceProvider>();

        await scope.EnlistProviderAsync(provider, CancellationToken.None).ConfigureAwait(false);
        await scope.EnlistProviderAsync(provider, CancellationToken.None).ConfigureAwait(false);

        var providers = scope.GetEnlistedProviders().ToList();
        providers.Count.ShouldBe(1);
    }

    [Fact]
    public void OnCommit_WithNull_ShouldThrow()
    {
        var scope = new TestTransactionScope();
        Should.Throw<ArgumentNullException>(() => scope.OnCommit(null!));
    }

    [Fact]
    public void OnRollback_WithNull_ShouldThrow()
    {
        var scope = new TestTransactionScope();
        Should.Throw<ArgumentNullException>(() => scope.OnRollback(null!));
    }

    [Fact]
    public void OnComplete_WithNull_ShouldThrow()
    {
        var scope = new TestTransactionScope();
        Should.Throw<ArgumentNullException>(() => scope.OnComplete(null!));
    }

    [Fact]
    public async Task OnCommit_AfterDispose_ShouldThrow()
    {
        var scope = new TestTransactionScope();
        await scope.DisposeAsync().ConfigureAwait(false);

        Should.Throw<ObjectDisposedException>(() => scope.OnCommit(() => Task.CompletedTask));
    }

    [Fact]
    public async Task CommitAsync_AfterDispose_ShouldThrow()
    {
        var scope = new TestTransactionScope();
        await scope.DisposeAsync().ConfigureAwait(false);

        await Should.ThrowAsync<ObjectDisposedException>(
            () => scope.CommitAsync(CancellationToken.None)).ConfigureAwait(false);
    }

    [Fact]
    public async Task CommitAsync_AfterCommit_ShouldThrowNotActive()
    {
        var scope = new TestTransactionScope();
        await scope.CommitAsync(CancellationToken.None).ConfigureAwait(false);

        // After commit, status is Committed, not Active
        await Should.ThrowAsync<InvalidOperationException>(
            () => scope.CommitAsync(CancellationToken.None)).ConfigureAwait(false);
    }

    [Fact]
    public void TransactionId_ShouldBeUnique()
    {
        var scope1 = new TestTransactionScope();
        var scope2 = new TestTransactionScope();

        scope1.TransactionId.ShouldNotBe(scope2.TransactionId);
    }

    [Fact]
    public void Constructor_WithCustomIsolationLevel_ShouldApply()
    {
        var scope = new TestTransactionScope(IsolationLevel.Serializable);
        scope.IsolationLevel.ShouldBe(IsolationLevel.Serializable);
    }

    [Fact]
    public void Dispose_ShouldSetDisposedFlag()
    {
        var scope = new TestTransactionScope();
        scope.Dispose();
        scope.DisposedFlag.ShouldBeTrue();
    }

    [Fact]
    public async Task MultipleCallbacks_ShouldAllExecute()
    {
        var scope = new TestTransactionScope();
        var counter = 0;

        scope.OnCommit(() => { counter++; return Task.CompletedTask; });
        scope.OnCommit(() => { counter++; return Task.CompletedTask; });
        scope.OnCommit(() => { counter++; return Task.CompletedTask; });

        await scope.CommitAsync(CancellationToken.None).ConfigureAwait(false);

        counter.ShouldBe(3);
    }

    [Fact]
    public async Task Timeout_ShouldThrowWhenExpired()
    {
        // Create a scope with very short timeout
        var scope = new TestTransactionScope(timeout: TimeSpan.FromMilliseconds(1));

        // Wait for timeout to expire
        await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50).ConfigureAwait(false);

        await Should.ThrowAsync<TimeoutException>(
            () => scope.CommitAsync(CancellationToken.None)).ConfigureAwait(false);

        scope.Status.ShouldBe(TransactionStatus.TimedOut);
    }
}
