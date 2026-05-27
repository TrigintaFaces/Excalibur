// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Tests.Abstractions;

/// <summary>
/// Tests for <see cref="IPageableProjectionStore{TProjection}"/> ISP sub-interface (bd-d5djpa).
/// Verifies interface shape, ISP inheritance from <see cref="IProjectionStore{TProjection}"/>,
/// generic constraint, and implementability with a concrete test double.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class IPageableProjectionStoreShould
{
    #region ISP Inheritance

    [Fact]
    public void InheritFromIProjectionStore()
    {
        // Assert — IPageableProjectionStore<T> must extend IProjectionStore<T>
        var type = typeof(IPageableProjectionStore<>);
        var baseInterface = typeof(IProjectionStore<>);

        type.GetInterfaces().ShouldContain(
            i => i.IsGenericType && i.GetGenericTypeDefinition() == baseInterface,
            "IPageableProjectionStore<T> must inherit from IProjectionStore<T>");
    }

    [Fact]
    public void BeAnInterface()
    {
        // Assert
        typeof(IPageableProjectionStore<>).IsInterface.ShouldBeTrue();
    }

    [Fact]
    public void HaveReferenceTypeConstraint()
    {
        // Assert — TProjection must be constrained to class
        var typeParam = typeof(IPageableProjectionStore<>).GetGenericArguments()[0];
        var constraints = typeParam.GenericParameterAttributes;

        constraints.ShouldSatisfyAllConditions(
            () => (constraints & System.Reflection.GenericParameterAttributes.ReferenceTypeConstraint)
                .ShouldNotBe((System.Reflection.GenericParameterAttributes)0,
                    "TProjection must have 'where TProjection : class' constraint"));
    }

    #endregion ISP Inheritance

    #region Method Shape

    [Fact]
    public void DefineQueryPagedAsyncMethod()
    {
        // Arrange
        var method = typeof(IPageableProjectionStore<TestProjection>).GetMethod(nameof(IPageableProjectionStore<TestProjection>.QueryPagedAsync));

        // Assert
        method.ShouldNotBeNull("IPageableProjectionStore<T> must define QueryPagedAsync");
        method.ReturnType.ShouldBe(typeof(Task<PagedResult<TestProjection>>));
    }

    [Fact]
    public void QueryPagedAsyncHasCorrectParameters()
    {
        // Arrange
        var method = typeof(IPageableProjectionStore<TestProjection>).GetMethod(nameof(IPageableProjectionStore<TestProjection>.QueryPagedAsync));

        // Assert
        method.ShouldNotBeNull();
        var parameters = method.GetParameters();
        parameters.Length.ShouldBe(5);
        parameters[0].ParameterType.ShouldBe(typeof(IDictionary<string, object>));
        parameters[0].Name.ShouldBe("filters");
        parameters[1].ParameterType.ShouldBe(typeof(int));
        parameters[1].Name.ShouldBe("pageNumber");
        parameters[2].ParameterType.ShouldBe(typeof(int));
        parameters[2].Name.ShouldBe("pageSize");
        parameters[3].ParameterType.ShouldBe(typeof(QueryOptions));
        parameters[3].Name.ShouldBe("options");
        parameters[4].ParameterType.ShouldBe(typeof(CancellationToken));
        parameters[4].Name.ShouldBe("cancellationToken");
    }

    [Fact]
    public void HaveExactlyOneOwnMethod()
    {
        // Assert — ISP: only QueryPagedAsync is declared on this interface (not inherited)
        var ownMethods = typeof(IPageableProjectionStore<TestProjection>)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);

        ownMethods.Length.ShouldBe(1, "ISP sub-interface should declare exactly one method");
        ownMethods[0].Name.ShouldBe(nameof(IPageableProjectionStore<TestProjection>.QueryPagedAsync));
    }

    #endregion Method Shape

    #region Implementability

    [Fact]
    public async Task BeImplementableByConcreteClass()
    {
        // Arrange
        var store = new TestPageableStore();
        var filters = new Dictionary<string, object> { ["Status"] = "Active" };

        // Act
        var result = await store.QueryPagedAsync(filters, 1, 10, null, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(1);
        result.PageNumber.ShouldBe(1);
        result.PageSize.ShouldBe(10);
        result.TotalItems.ShouldBe(1);
    }

    [Fact]
    public void BeDetectableViaPatternMatching()
    {
        // Arrange — ISP pattern: consumers check via 'is' cast
        IProjectionStore<TestProjection> store = new TestPageableStore();

        // Act
        var isPageable = store is IPageableProjectionStore<TestProjection>;

        // Assert
        isPageable.ShouldBeTrue("Store implementing IPageableProjectionStore should be detectable via pattern match");
    }

    [Fact]
    public void NotBeDetectableWhenNotImplemented()
    {
        // Arrange — a plain IProjectionStore that doesn't implement IPageableProjectionStore
        IProjectionStore<TestProjection> store = new PlainProjectionStore();

        // Act
        var isPageable = store is IPageableProjectionStore<TestProjection>;

        // Assert
        isPageable.ShouldBeFalse("Plain IProjectionStore should not be detectable as IPageableProjectionStore");
    }

    [Fact]
    public async Task InheritBaseStoreMethodsFromIProjectionStore()
    {
        // Arrange — verify base interface methods are accessible through the sub-interface
        IPageableProjectionStore<TestProjection> store = new TestPageableStore();

        // Act — call a method from IProjectionStore<T> through the sub-interface
        var projection = await store.GetByIdAsync("test-1", CancellationToken.None).ConfigureAwait(false);

        // Assert
        projection.ShouldNotBeNull();
    }

    #endregion Implementability

    #region Test Doubles

    private sealed class TestProjection
    {
        public string Id { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
    }

    /// <summary>Plain store that does NOT implement IPageableProjectionStore.</summary>
    private sealed class PlainProjectionStore : IProjectionStore<TestProjection>
    {
        public Task<TestProjection?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<TestProjection?>(null);
        public Task UpsertAsync(string id, TestProjection projection, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<TestProjection>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TestProjection>>(Array.Empty<TestProjection>());
        public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) => Task.FromResult(0L);
    }

    private sealed class TestPageableStore : IPageableProjectionStore<TestProjection>
    {
        private static readonly TestProjection DefaultProjection = new() { Id = "test-1", Status = "Active" };

        public Task<PagedResult<TestProjection>> QueryPagedAsync(
            IDictionary<string, object>? filters,
            int pageNumber,
            int pageSize,
            QueryOptions? options,
            CancellationToken cancellationToken)
        {
            var items = new[] { DefaultProjection };
            return Task.FromResult(new PagedResult<TestProjection>(items, pageNumber, pageSize, 1));
        }

        public Task<TestProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
            => Task.FromResult<TestProjection?>(DefaultProjection);

        public Task UpsertAsync(string id, TestProjection projection, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task DeleteAsync(string id, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<IReadOnlyList<TestProjection>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<TestProjection>>(new[] { DefaultProjection });

        public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken)
            => Task.FromResult(1L);
    }

    #endregion Test Doubles
}
