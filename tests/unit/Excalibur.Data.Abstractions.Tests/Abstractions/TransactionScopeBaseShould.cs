using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TransactionScopeBaseShould
{
    [Fact]
    public void InitializeWithCorrectDefaults()
    {
        // Arrange & Act
        using var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromSeconds(30));

        // Assert
        scope.TransactionId.ShouldNotBeNullOrWhiteSpace();
        scope.IsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
        scope.Status.ShouldBe(TransactionStatus.Active);
        scope.StartTime.ShouldNotBe(default);
        scope.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void GenerateUniqueTransactionIds()
    {
        // Arrange & Act
        using var scope1 = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromSeconds(30));
        using var scope2 = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromSeconds(30));

        // Assert
        scope1.TransactionId.ShouldNotBe(scope2.TransactionId);
    }

    [Fact]
    public void ReturnEmptyProvidersInitially()
    {
        // Arrange
        using var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromSeconds(30));

        // Act
        var providers = scope.GetEnlistedProviders();

        // Assert
        providers.ShouldBeEmpty();
    }

    [Fact]
    public void RegisterCommitCallback()
    {
        // Arrange
        using var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromSeconds(30));
        var called = false;

        // Act
        scope.OnCommit(() =>
        {
            called = true;
            return Task.CompletedTask;
        });

        // Assert - callback registered but not executed yet
        called.ShouldBeFalse();
    }

    [Fact]
    public void RegisterRollbackCallback()
    {
        // Arrange
        using var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromSeconds(30));
        var called = false;

        // Act
        scope.OnRollback(() =>
        {
            called = true;
            return Task.CompletedTask;
        });

        // Assert
        called.ShouldBeFalse();
    }

    [Fact]
    public void RegisterCompleteCallback()
    {
        // Arrange
        using var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromSeconds(30));
        TransactionStatus? receivedStatus = null;

        // Act
        scope.OnComplete(status =>
        {
            receivedStatus = status;
            return Task.CompletedTask;
        });

        // Assert
        receivedStatus.ShouldBeNull();
    }

    [Fact]
    public void ThrowOnNullCommitCallback()
    {
        // Arrange
        using var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromSeconds(30));

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => scope.OnCommit(null!));
    }

    [Fact]
    public void ThrowOnNullRollbackCallback()
    {
        // Arrange
        using var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromSeconds(30));

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => scope.OnRollback(null!));
    }

    [Fact]
    public void ThrowOnNullCompleteCallback()
    {
        // Arrange
        using var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromSeconds(30));

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => scope.OnComplete(null!));
    }

    [Fact]
    public void AllowTimeoutModification()
    {
        // Arrange
        using var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromSeconds(30));
        var newTimeout = TimeSpan.FromMinutes(5);

        // Act
        scope.Timeout = newTimeout;

        // Assert
        scope.Timeout.ShouldBe(newTimeout);
    }

    [Fact]
    public async Task ExecuteCommitCallbacksSuccessfully()
    {
        // Arrange
        using var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromSeconds(30));
        var callbackExecuted = false;
        scope.OnCommit(() =>
        {
            callbackExecuted = true;
            return Task.CompletedTask;
        });

        // Act
        var errors = await scope.InvokeExecuteCommitCallbacksAsync();

        // Assert
        errors.ShouldBeNull();
        callbackExecuted.ShouldBeTrue();
    }

    [Fact]
    public async Task CollectCommitCallbackErrors()
    {
        // Arrange
        using var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromSeconds(30));
        scope.OnCommit(() => throw new InvalidOperationException("Callback failed"));

        // Act
        var errors = await scope.InvokeExecuteCommitCallbacksAsync();

        // Assert
        errors.ShouldNotBeNull();
        errors!.Count.ShouldBe(1);
        errors[0].ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteRollbackCallbacksSuccessfully()
    {
        // Arrange
        using var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromSeconds(30));
        var callbackExecuted = false;
        scope.OnRollback(() =>
        {
            callbackExecuted = true;
            return Task.CompletedTask;
        });

        // Act
        var errors = await scope.InvokeExecuteRollbackCallbacksAsync();

        // Assert
        errors.ShouldBeNull();
        callbackExecuted.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteCompleteCallbacksWithStatus()
    {
        // Arrange
        using var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromSeconds(30));
        TransactionStatus? receivedStatus = null;
        scope.OnComplete(status =>
        {
            receivedStatus = status;
            return Task.CompletedTask;
        });

        // Act
        var errors = await scope.InvokeExecuteCompleteCallbacksAsync(null);

        // Assert
        errors.ShouldBeNull();
        receivedStatus.ShouldBe(TransactionStatus.Active);
    }

    [Fact]
    public void EnlistProviderSuccessfully()
    {
        // Arrange
        using var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromSeconds(30));
        var provider = A.Fake<IPersistenceProvider>();

        // Act
        var result = scope.InvokeTryEnlistProvider(provider);

        // Assert
        result.ShouldBeTrue();
        scope.GetEnlistedProviders().ShouldContain(provider);
    }

    [Fact]
    public void RejectDuplicateProviderEnlistment()
    {
        // Arrange
        using var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromSeconds(30));
        var provider = A.Fake<IPersistenceProvider>();

        // Act
        var first = scope.InvokeTryEnlistProvider(provider);
        var second = scope.InvokeTryEnlistProvider(provider);

        // Assert
        first.ShouldBeTrue();
        second.ShouldBeFalse();
    }

    [Fact]
    public void ThrowOnCallbackRegistrationAfterDispose()
    {
        // Arrange
        var scope = new TestTransactionScope(IsolationLevel.ReadCommitted, TimeSpan.FromSeconds(30));
        scope.Dispose();

        // Act & Assert
        Should.Throw<ObjectDisposedException>(() => scope.OnCommit(() => Task.CompletedTask));
    }

    /// <summary>
    /// Concrete implementation for testing the abstract TransactionScopeBase.
    /// </summary>
    private sealed class TestTransactionScope : TransactionScopeBase
    {
        public TestTransactionScope(IsolationLevel isolationLevel, TimeSpan timeout)
            : base(isolationLevel, timeout)
        {
        }

        public override Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task EnlistProviderAsync(IPersistenceProvider provider, CancellationToken cancellationToken)
        {
            TryEnlistProvider(provider);
            return Task.CompletedTask;
        }

        public override Task EnlistConnectionAsync(IDbConnection connection, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public override ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
        }

        // Expose protected methods for testing
        public bool InvokeTryEnlistProvider(IPersistenceProvider provider) => TryEnlistProvider(provider);

        public Task<List<Exception>?> InvokeExecuteCommitCallbacksAsync() => ExecuteCommitCallbacksAsync();

        public Task<List<Exception>?> InvokeExecuteRollbackCallbacksAsync() => ExecuteRollbackCallbacksAsync();

        public Task<List<Exception>?> InvokeExecuteCompleteCallbacksAsync(List<Exception>? errors) =>
            ExecuteCompleteCallbacksAsync(errors);
    }
}
