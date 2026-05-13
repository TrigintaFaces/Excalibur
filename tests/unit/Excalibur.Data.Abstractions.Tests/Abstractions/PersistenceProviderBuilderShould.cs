// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.Tests.Abstractions.Persistence;

/// <summary>
/// Unit tests for <see cref="PersistenceProviderBuilder"/>.
/// Validates builder pattern: null guards, decorator ordering, chaining, zero-decorator case.
/// </summary>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Data)]
[Trait(TraitNames.Feature, TestFeatures.Abstractions)]
public sealed class PersistenceProviderBuilderShould : UnitTestBase
{
    private static readonly string[] ExpectedCallOrder = ["first", "second"];

    private readonly IPersistenceProvider _innerProvider;

    public PersistenceProviderBuilderShould()
    {
        _innerProvider = A.Fake<IPersistenceProvider>();
        A.CallTo(() => _innerProvider.Name).Returns("InnerProvider");
    }

    // ========================================
    // Constructor — Null Guard
    // ========================================

    [Fact]
    public void ThrowArgumentNullException_WhenInnerProviderIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new PersistenceProviderBuilder(null!));
    }

    // ========================================
    // Use — Null Guard
    // ========================================

    [Fact]
    public void ThrowArgumentNullException_WhenDecoratorIsNull()
    {
        // Arrange
        var builder = new PersistenceProviderBuilder(_innerProvider);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => builder.Use(null!));
    }

    // ========================================
    // Build — Zero Decorators
    // ========================================

    [Fact]
    public void ReturnInnerProvider_WhenNoDecoratorsRegistered()
    {
        // Arrange
        var builder = new PersistenceProviderBuilder(_innerProvider);

        // Act
        var result = builder.Build();

        // Assert
        result.ShouldBeSameAs(_innerProvider);
    }

    // ========================================
    // Build — Single Decorator
    // ========================================

    [Fact]
    public void WrapInnerProvider_WithSingleDecorator()
    {
        // Arrange
        var wrapper = A.Fake<IPersistenceProvider>();
        A.CallTo(() => wrapper.Name).Returns("Wrapper");

        var builder = new PersistenceProviderBuilder(_innerProvider)
            .Use(_ => wrapper);

        // Act
        var result = builder.Build();

        // Assert
        result.ShouldBeSameAs(wrapper);
    }

    // ========================================
    // Build — Decorator Ordering (first = outermost)
    // ========================================

    [Fact]
    public void ApplyDecorators_InRegistrationOrder_FirstIsOutermost()
    {
        // Arrange — track the wrapping order
        var callOrder = new List<string>();

        var builder = new PersistenceProviderBuilder(_innerProvider)
            .Use(inner =>
            {
                callOrder.Add("first");
                var wrapper = A.Fake<IPersistenceProvider>();
                A.CallTo(() => wrapper.Name).Returns("First-wrapping-" + inner.Name);
                return wrapper;
            })
            .Use(inner =>
            {
                callOrder.Add("second");
                var wrapper = A.Fake<IPersistenceProvider>();
                A.CallTo(() => wrapper.Name).Returns("Second-wrapping-" + inner.Name);
                return wrapper;
            });

        // Act
        var result = builder.Build();

        // Assert — first decorator called first, second wraps the first's result
        callOrder.ShouldBe(ExpectedCallOrder);
        result.Name.ShouldStartWith("Second-wrapping-First-wrapping-");
    }

    // ========================================
    // Fluent Chaining
    // ========================================

    [Fact]
    public void ReturnSameBuilder_ForFluentChaining()
    {
        // Arrange
        var builder = new PersistenceProviderBuilder(_innerProvider);

        // Act
        var returned = builder
            .Use(inner => inner)
            .Use(inner => inner);

        // Assert
        returned.ShouldBeSameAs(builder);
    }

    // ========================================
    // Edge Cases
    // ========================================

    [Fact]
    public void PassInnerProvider_ToFirstDecorator()
    {
        // Arrange
        IPersistenceProvider? received = null;
        var builder = new PersistenceProviderBuilder(_innerProvider)
            .Use(inner =>
            {
                received = inner;
                return inner;
            });

        // Act
        builder.Build();

        // Assert
        received.ShouldBeSameAs(_innerProvider);
    }

    [Fact]
    public void SupportMultipleBuilds_WithSameConfiguration()
    {
        // Arrange
        var builder = new PersistenceProviderBuilder(_innerProvider);
        // no decorators

        // Act
        var result1 = builder.Build();
        var result2 = builder.Build();

        // Assert — both return the same inner
        result1.ShouldBeSameAs(_innerProvider);
        result2.ShouldBeSameAs(_innerProvider);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            (_innerProvider as IDisposable)?.Dispose();
        }

        base.Dispose(disposing);
    }
}
