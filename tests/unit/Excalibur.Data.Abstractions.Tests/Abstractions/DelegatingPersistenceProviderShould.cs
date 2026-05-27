#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy needs stored ValueTask

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Persistence;

namespace Excalibur.Data.Tests.Abstractions.Persistence;

/// <summary>
/// Unit tests for <see cref="DelegatingPersistenceProvider"/>.
/// Validates decorator pattern: null-guard, delegation forwarding, disposal chain.
/// </summary>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Data)]
[Trait(TraitNames.Feature, TestFeatures.Abstractions)]
public sealed class DelegatingPersistenceProviderShould : UnitTestBase, IAsyncDisposable
{
    private readonly IPersistenceProvider _innerProvider;
    private readonly TestDelegatingProvider _sut;

    public DelegatingPersistenceProviderShould()
    {
        _innerProvider = A.Fake<IPersistenceProvider>();
        _sut = new TestDelegatingProvider(_innerProvider);
    }

    // ========================================
    // Constructor — Null Guard
    // ========================================

    [Fact]
    public void ThrowArgumentNullException_WhenInnerIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new TestDelegatingProvider(null!))
            .ParamName.ShouldBe("inner");
    }

    // ========================================
    // Property Delegation
    // ========================================

    [Fact]
    public void DelegateName_ToInnerProvider()
    {
        // Arrange
        A.CallTo(() => _innerProvider.Name).Returns("SqlServer");

        // Act & Assert
        _sut.Name.ShouldBe("SqlServer");
    }

    [Fact]
    public void DelegateProviderType_ToInnerProvider()
    {
        // Arrange
        A.CallTo(() => _innerProvider.ProviderType).Returns("SQL");

        // Act & Assert
        _sut.ProviderType.ShouldBe("SQL");
    }

    // ========================================
    // Method Delegation
    // ========================================

    [Fact]
    public async Task DelegateExecuteAsync_ToInnerProvider()
    {
        // Arrange
        var request = A.Fake<IDataRequest<IDbConnection, int>>();
        A.CallTo(() => _innerProvider.ExecuteAsync(request, A<CancellationToken>.Ignored))
            .Returns(42);

        // Act
        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        // Assert
        result.ShouldBe(42);
        A.CallTo(() => _innerProvider.ExecuteAsync(request, A<CancellationToken>.Ignored))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DelegateInitializeAsync_ToInnerProvider()
    {
        // Arrange
        var options = A.Fake<IPersistenceOptions>();

        // Act
        await _sut.InitializeAsync(options, CancellationToken.None);

        // Assert
        A.CallTo(() => _innerProvider.InitializeAsync(options, A<CancellationToken>.Ignored))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void DelegateGetService_ToInnerProvider()
    {
        // Arrange
        var expectedService = new object();
        A.CallTo(() => _innerProvider.GetService(typeof(IPersistenceProviderHealth)))
            .Returns(expectedService);

        // Act
        var result = _sut.GetService(typeof(IPersistenceProviderHealth));

        // Assert
        result.ShouldBe(expectedService);
    }

    [Fact]
    public void ReturnNull_WhenInnerGetServiceReturnsNull()
    {
        // Arrange
        A.CallTo(() => _innerProvider.GetService(A<Type>.Ignored)).Returns(null);

        // Act
        var result = _sut.GetService(typeof(string));

        // Assert
        result.ShouldBeNull();
    }

    // ========================================
    // Disposal Chain
    // ========================================

    [Fact]
    public async Task DelegateDisposeAsync_ToInnerProvider()
    {
        // Arrange
        A.CallTo(() => _innerProvider.DisposeAsync()).Returns(ValueTask.CompletedTask);

        // Act
        await _sut.DisposeAsync();

        // Assert
        A.CallTo(() => _innerProvider.DisposeAsync()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void DelegateDispose_ToInnerProvider()
    {
        // Act
        _sut.Dispose();

        // Assert
        A.CallTo(() => _innerProvider.Dispose()).MustHaveHappenedOnceExactly();
    }

    // ========================================
    // Edge Cases — CancellationToken Propagation
    // ========================================

    [Fact]
    public async Task PropagateCancellationToken_ToExecuteAsync()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var request = A.Fake<IDataRequest<IDbConnection, string>>();
        A.CallTo(() => _innerProvider.ExecuteAsync(request, cts.Token))
            .Returns("result");

        // Act
        var result = await _sut.ExecuteAsync(request, cts.Token);

        // Assert
        result.ShouldBe("result");
        A.CallTo(() => _innerProvider.ExecuteAsync(request, cts.Token))
            .MustHaveHappenedOnceExactly();
    }

    // ========================================
    // Concrete Test Subclass
    // ========================================

    public async ValueTask DisposeAsync()
    {
        await _sut.DisposeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            (_innerProvider as IDisposable)?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Concrete subclass exposing the abstract <see cref="DelegatingPersistenceProvider"/>
    /// for testing without overriding any methods (validates default delegation).
    /// </summary>
    private sealed class TestDelegatingProvider : DelegatingPersistenceProvider
    {
        public TestDelegatingProvider(IPersistenceProvider inner) : base(inner) { }
    }
}
