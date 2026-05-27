// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Tests.Abstractions;

/// <summary>
/// Tests for <see cref="ICursorProjectionStore{TProjection}"/> ISP sub-interface (bd-vdp5xk).
/// Verifies interface shape, ISP inheritance from <see cref="IProjectionStore{TProjection}"/>,
/// generic constraint, and implementability with a concrete test double.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ICursorProjectionStoreShould
{
    #region ISP Inheritance

    [Fact]
    public void InheritFromIProjectionStore()
    {
        // Assert — ICursorProjectionStore<T> must extend IProjectionStore<T>
        var type = typeof(ICursorProjectionStore<>);
        var baseInterface = typeof(IProjectionStore<>);

        type.GetInterfaces().ShouldContain(
            i => i.IsGenericType && i.GetGenericTypeDefinition() == baseInterface,
            "ICursorProjectionStore<T> must inherit from IProjectionStore<T>");
    }

    [Fact]
    public void BeAnInterface()
    {
        // Assert
        typeof(ICursorProjectionStore<>).IsInterface.ShouldBeTrue();
    }

    [Fact]
    public void HaveReferenceTypeConstraint()
    {
        // Assert — TProjection must be constrained to class
        var typeParam = typeof(ICursorProjectionStore<>).GetGenericArguments()[0];
        var constraints = typeParam.GenericParameterAttributes;

        constraints.ShouldSatisfyAllConditions(
            () => (constraints & System.Reflection.GenericParameterAttributes.ReferenceTypeConstraint)
                .ShouldNotBe((System.Reflection.GenericParameterAttributes)0,
                    "TProjection must have 'where TProjection : class' constraint"));
    }

    #endregion ISP Inheritance

    #region Method Shape

    [Fact]
    public void DefineQueryCursorAsyncMethod()
    {
        // Arrange
        var method = typeof(ICursorProjectionStore<TestProjection>).GetMethod(nameof(ICursorProjectionStore<TestProjection>.QueryCursorAsync));

        // Assert
        method.ShouldNotBeNull("ICursorProjectionStore<T> must define QueryCursorAsync");
        method.ReturnType.ShouldBe(typeof(Task<CursorPagedResult<TestProjection>>));
    }

    [Fact]
    public void QueryCursorAsyncHasCorrectParameters()
    {
        // Arrange
        var method = typeof(ICursorProjectionStore<TestProjection>).GetMethod(nameof(ICursorProjectionStore<TestProjection>.QueryCursorAsync));

        // Assert
        method.ShouldNotBeNull();
        var parameters = method.GetParameters();
        parameters.Length.ShouldBe(4);
        parameters[0].ParameterType.ShouldBe(typeof(IDictionary<string, object>));
        parameters[0].Name.ShouldBe("filters");
        parameters[1].ParameterType.ShouldBe(typeof(string));
        parameters[1].Name.ShouldBe("cursor");
        parameters[2].ParameterType.ShouldBe(typeof(int));
        parameters[2].Name.ShouldBe("pageSize");
        parameters[3].ParameterType.ShouldBe(typeof(CancellationToken));
        parameters[3].Name.ShouldBe("cancellationToken");
    }

    [Fact]
    public void HaveExactlyOneOwnMethod()
    {
        // Assert — ISP: only QueryCursorAsync is declared on this interface
        var ownMethods = typeof(ICursorProjectionStore<TestProjection>)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);

        ownMethods.Length.ShouldBe(1, "ISP sub-interface should declare exactly one method");
        ownMethods[0].Name.ShouldBe(nameof(ICursorProjectionStore<TestProjection>.QueryCursorAsync));
    }

    #endregion Method Shape

    #region Implementability

    [Fact]
    public async Task BeImplementableByConcreteClass()
    {
        // Arrange
        var store = new TestCursorStore();

        // Act
        var result = await store.QueryCursorAsync(null, null, 10, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.ShouldNotBeNull();
        result.Items.ShouldNotBeNull();
        result.PageSize.ShouldBe(10);
    }

    [Fact]
    public async Task ReturnCursorForNextPage()
    {
        // Arrange
        var store = new TestCursorStore();

        // Act — first page
        var result = await store.QueryCursorAsync(null, null, 1, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.HasMore.ShouldBeTrue();
        result.NextCursor.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ReturnNullCursorOnLastPage()
    {
        // Arrange
        var store = new TestCursorStore();

        // Act — request with cursor pointing to last page
        var result = await store.QueryCursorAsync(null, "last-page", 10, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.HasMore.ShouldBeFalse();
        result.NextCursor.ShouldBeNull();
    }

    [Fact]
    public void BeDetectableViaPatternMatching()
    {
        // Arrange — ISP pattern: consumers check via 'is' cast (IBufferDistributedCache precedent)
        IProjectionStore<TestProjection> store = new TestCursorStore();

        // Act
        var isCursor = store is ICursorProjectionStore<TestProjection>;

        // Assert
        isCursor.ShouldBeTrue("Store implementing ICursorProjectionStore should be detectable via pattern match");
    }

    [Fact]
    public void NotBeDetectableWhenNotImplemented()
    {
        // Arrange — a plain IProjectionStore that doesn't implement ICursorProjectionStore
        IProjectionStore<TestProjection> store = new PlainProjectionStore();

        // Act
        var isCursor = store is ICursorProjectionStore<TestProjection>;

        // Assert
        isCursor.ShouldBeFalse("Plain IProjectionStore should not be detectable as ICursorProjectionStore");
    }

    [Fact]
    public async Task InheritBaseStoreMethodsFromIProjectionStore()
    {
        // Arrange
        ICursorProjectionStore<TestProjection> store = new TestCursorStore();

        // Act — call a method from IProjectionStore<T> through the sub-interface
        var count = await store.CountAsync(null, CancellationToken.None).ConfigureAwait(false);

        // Assert
        count.ShouldBe(2L);
    }

    [Fact]
    public void NotInheritFromIPageableProjectionStore()
    {
        // Assert — ICursorProjectionStore is a separate ISP branch, not derived from IPageableProjectionStore
        var type = typeof(ICursorProjectionStore<>);
        type.GetInterfaces().ShouldNotContain(
            i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPageableProjectionStore<>),
            "ICursorProjectionStore should not inherit from IPageableProjectionStore (separate ISP branches)");
    }

    #endregion Implementability

    #region Dual Implementation

    [Fact]
    public void AllowDualImplementation()
    {
        // Arrange — a store can implement both pagination styles
        var store = new TestDualPaginationStore();

        // Assert
        (store is IPageableProjectionStore<TestProjection>).ShouldBeTrue();
        (store is ICursorProjectionStore<TestProjection>).ShouldBeTrue();
    }

    #endregion Dual Implementation

    #region Test Doubles

    private sealed class TestProjection
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
    }

    /// <summary>Plain store that does NOT implement ICursorProjectionStore.</summary>
    private sealed class PlainProjectionStore : IProjectionStore<TestProjection>
    {
        public Task<TestProjection?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<TestProjection?>(null);
        public Task UpsertAsync(string id, TestProjection projection, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<TestProjection>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TestProjection>>(Array.Empty<TestProjection>());
        public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) => Task.FromResult(0L);
    }

    private sealed class TestCursorStore : ICursorProjectionStore<TestProjection>
    {
        private static readonly TestProjection[] Items =
        [
            new() { Id = "1", Name = "First" },
            new() { Id = "2", Name = "Second" },
        ];

        public Task<CursorPagedResult<TestProjection>> QueryCursorAsync(
            IDictionary<string, object>? filters,
            string? cursor,
            int pageSize,
            CancellationToken cancellationToken)
        {
            if (cursor == "last-page")
            {
                return Task.FromResult(new CursorPagedResult<TestProjection>(
                    Array.Empty<TestProjection>(), pageSize, 2, null));
            }

            return Task.FromResult(new CursorPagedResult<TestProjection>(
                Items.Take(pageSize), pageSize, 2, "next-cursor-token"));
        }

        public Task<TestProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
            => Task.FromResult<TestProjection?>(Items.FirstOrDefault(i => i.Id == id));

        public Task UpsertAsync(string id, TestProjection projection, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task DeleteAsync(string id, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<IReadOnlyList<TestProjection>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<TestProjection>>(Items);

        public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken)
            => Task.FromResult(2L);
    }

    private sealed class TestDualPaginationStore : IPageableProjectionStore<TestProjection>, ICursorProjectionStore<TestProjection>
    {
        public Task<PagedResult<TestProjection>> QueryPagedAsync(IDictionary<string, object>? filters, int pageNumber, int pageSize, QueryOptions? options, CancellationToken cancellationToken)
            => Task.FromResult(new PagedResult<TestProjection>(Array.Empty<TestProjection>()));

        public Task<CursorPagedResult<TestProjection>> QueryCursorAsync(IDictionary<string, object>? filters, string? cursor, int pageSize, CancellationToken cancellationToken)
            => Task.FromResult(new CursorPagedResult<TestProjection>(Array.Empty<TestProjection>(), pageSize, 0));

        public Task<TestProjection?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<TestProjection?>(null);
        public Task UpsertAsync(string id, TestProjection projection, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<TestProjection>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TestProjection>>(Array.Empty<TestProjection>());
        public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) => Task.FromResult(0L);
    }

    #endregion Test Doubles
}
