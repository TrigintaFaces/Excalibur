// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.Tests.Abstractions;

// ── Concrete test decorator ──

public class LoggingPersistenceProvider : DelegatingPersistenceProvider
{
    public List<string> LogEntries { get; } = [];

    public LoggingPersistenceProvider(IPersistenceProvider innerProvider)
        : base(innerProvider)
    {
    }

    public override async Task<TResult> ExecuteAsync<TConnection, TResult>(
        IDataRequest<TConnection, TResult> request,
        CancellationToken cancellationToken)
    {
        LogEntries.Add($"Before Execute: {request.GetType().Name}");
        var result = await base.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        LogEntries.Add($"After Execute: {request.GetType().Name}");
        return result;
    }

    public override async Task InitializeAsync(IPersistenceOptions options, CancellationToken cancellationToken)
    {
        LogEntries.Add("Before Initialize");
        await base.InitializeAsync(options, cancellationToken).ConfigureAwait(false);
        LogEntries.Add("After Initialize");
    }
}

public class CountingPersistenceProvider : DelegatingPersistenceProvider
{
    public int ExecuteCount { get; private set; }
    public int InitializeCount { get; private set; }

    public CountingPersistenceProvider(IPersistenceProvider innerProvider)
        : base(innerProvider)
    {
    }

    public override async Task<TResult> ExecuteAsync<TConnection, TResult>(
        IDataRequest<TConnection, TResult> request,
        CancellationToken cancellationToken)
    {
        ExecuteCount++;
        return await base.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public override async Task InitializeAsync(IPersistenceOptions options, CancellationToken cancellationToken)
    {
        InitializeCount++;
        await base.InitializeAsync(options, cancellationToken).ConfigureAwait(false);
    }
}

[Trait("Category", "Unit")]
public class DelegatingPersistenceProviderFunctionalShould
{
    [Fact]
    public void Constructor_WithNull_ShouldThrow()
    {
        Should.Throw<ArgumentNullException>(() => new LoggingPersistenceProvider(null!));
    }

    [Fact]
    public void Name_ShouldDelegateToInner()
    {
        var inner = A.Fake<IPersistenceProvider>();
        A.CallTo(() => inner.Name).Returns("TestProvider");

        var decorator = new LoggingPersistenceProvider(inner);

        decorator.Name.ShouldBe("TestProvider");
    }

    [Fact]
    public void ProviderType_ShouldDelegateToInner()
    {
        var inner = A.Fake<IPersistenceProvider>();
        A.CallTo(() => inner.ProviderType).Returns("SqlServer");

        var decorator = new LoggingPersistenceProvider(inner);

        decorator.ProviderType.ShouldBe("SqlServer");
    }

    [Fact]
    public async Task InitializeAsync_ShouldDelegateToInner()
    {
        var inner = A.Fake<IPersistenceProvider>();
        var options = A.Fake<IPersistenceOptions>();
        var decorator = new LoggingPersistenceProvider(inner);

        await decorator.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);

        A.CallTo(() => inner.InitializeAsync(options, CancellationToken.None)).MustHaveHappenedOnceExactly();
        decorator.LogEntries.ShouldContain("Before Initialize");
        decorator.LogEntries.ShouldContain("After Initialize");
    }

    [Fact]
    public void GetService_ShouldDelegateToInner()
    {
        var inner = A.Fake<IPersistenceProvider>();
        var expectedService = new object();
        A.CallTo(() => inner.GetService(typeof(object))).Returns(expectedService);

        var decorator = new LoggingPersistenceProvider(inner);

        decorator.GetService(typeof(object)).ShouldBe(expectedService);
    }

    [Fact]
    public void Dispose_ShouldDisposeInner()
    {
        var inner = A.Fake<IPersistenceProvider>();
        var decorator = new LoggingPersistenceProvider(inner);

        decorator.Dispose();

        A.CallTo(() => inner.Dispose()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeInner()
    {
        var inner = A.Fake<IPersistenceProvider>();
        var decorator = new LoggingPersistenceProvider(inner);

        await decorator.DisposeAsync().ConfigureAwait(false);

        A.CallTo(() => inner.DisposeAsync()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task StackedDecorators_ShouldChainCorrectly()
    {
        // Arrange
        var inner = A.Fake<IPersistenceProvider>();
        A.CallTo(() => inner.Name).Returns("Base");

        var logging = new LoggingPersistenceProvider(inner);
        var counting = new CountingPersistenceProvider(logging);
        var options = A.Fake<IPersistenceOptions>();

        // Act
        await counting.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);

        // Assert
        counting.InitializeCount.ShouldBe(1);
        logging.LogEntries.ShouldContain("Before Initialize");
        logging.LogEntries.ShouldContain("After Initialize");
        A.CallTo(() => inner.InitializeAsync(options, CancellationToken.None)).MustHaveHappenedOnceExactly();
    }
}
