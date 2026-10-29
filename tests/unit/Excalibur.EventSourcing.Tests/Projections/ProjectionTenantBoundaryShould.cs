// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Sharding;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Projections")]
[Trait("Pattern", "Regression")]
public sealed class ProjectionTenantBoundaryShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetireOnlyTheMatchingInlineSourceAndPartitionDuringRebuild(bool wrongTenant)
    {
        IProjectionStore<TenantBoundaryTestRow> inner = new InMemoryProjectionStore<TenantBoundaryTestRow>();
        var store = A.Fake<IProjectionStore<TenantBoundaryTestRow>>();
        A.CallTo(() => store.GetService(A<Type>._)).ReturnsLazily((Type type) => type == typeof(IProjectionTenantBoundary)
            ? ProjectionTenantBoundary.Partition(wrongTenant ? "B" : "A") : inner.GetService(type));
        var publicCheckpoints = new InMemorySubscriptionCheckpointStore();
        var inlineCheckpoints = new InMemorySubscriptionCheckpointStore();
        var key = SubscriptionKeys.ProjectionKey(typeof(TenantBoundaryTestRow));
        var identity = inner.GetService(typeof(IProjectionDestinationIdentity)).ShouldBeAssignableTo<IProjectionDestinationIdentity>().Identity;
        var cluster = inner.GetService(typeof(IProjectionDestinationScope)).ShouldBeAssignableTo<IProjectionDestinationScope>().Scope;
        var member = new SubscriptionMember(key, new string('A', 64), identity)
            { DestinationScope = ProjectionTenantValidation.Scope(ProjectionTenantBoundary.Partition("A"), cluster) };
        await inlineCheckpoints.BindSubscriptionAsync("Inline:orders", [member], CancellationToken.None);
        await publicCheckpoints.BindSubscriptionAsync("Inline:orders", [member], CancellationToken.None);
        var query = A.Fake<IGlobalStreamQuery>();
        A.CallTo(() => query.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]));
        A.CallTo(() => query.GetHeadPositionAsync(A<CancellationToken>._)).Returns(0L);
        var registry = new InMemoryProjectionRegistry();
        var builder = new ProjectionBuilder<TenantBoundaryTestRow>(registry);
        builder.Inline().When<TestOrderPlaced>((row, _) => row.Amount++);
        builder.Build();
        var services = new ServiceCollection();
        services.AddSingleton<IProjectionRegistry>(registry);
        services.AddSingleton(store);
        services.AddSingleton<ISubscriptionCheckpointStore>(publicCheckpoints);
        services.AddSingleton(new InlineProjectionSource("source", inlineCheckpoints, query));
        services.AddSingleton(Options.Create(new GlobalStreamProjectionOptions { ProjectionName = "orders" }));
        await using var provider = services.BuildServiceProvider();
        var sut = new ProjectionRebuildService(provider, A.Fake<IEventSerializer>(),
            Options.Create(new ProjectionRebuildOptions { OfflineRebuildAcknowledged = true }), NullLogger<ProjectionRebuildService>.Instance);
        if (wrongTenant)
        {
            await Should.ThrowAsync<InvalidOperationException>(() => sut.RebuildAsync<TenantBoundaryTestRow>(CancellationToken.None));
            (await inlineCheckpoints.ReadSubscriptionAsync("Inline:orders", CancellationToken.None)).Members.Count.ShouldBe(1);
        }
        else
        {
            await sut.RebuildAsync<TenantBoundaryTestRow>(CancellationToken.None);
            (await inlineCheckpoints.ReadSubscriptionAsync("Inline:orders", CancellationToken.None)).Members.ShouldBeEmpty();
        }
        (await publicCheckpoints.ReadSubscriptionAsync("Inline:orders", CancellationToken.None)).Members.Count.ShouldBe(1);
    }

    public static TheoryData<string?, string?, bool, int> BatchCases()
    {
        var result = new TheoryData<string?, string?, bool, int>();
        (string? Partition, string? Source, bool Succeeds)[] cases =
        [ ("Acme", "Acme", true), ("Acme", "acme", false), ("Acme", null, false),
          ("__untenanted__", "__untenanted__", true), ("__untenanted__", null, false), (null, "Other", true) ];
        foreach (var item in cases)
        {
            for (var path = 0; path < 3; path++)
            {
                result.Add(item.Partition, item.Source, item.Succeeds, path);
            }
        }
        return result;
    }

    [Theory]
    [MemberData(nameof(BatchCases))]
    public async Task ValidateTheWholeBatchBeforeInvokingAnyHandler(string? partition, string? source, bool succeeds, int path)
    {
        var inner = new InMemoryProjectionStore<TenantBoundaryTestRow>();
        var boundary = partition is null ? ProjectionTenantBoundary.EstateWide : ProjectionTenantBoundary.Partition(partition);
        var store = A.Fake<IProjectionStore<TenantBoundaryTestRow>>();
        A.CallTo(() => store.GetService(A<Type>._)).ReturnsLazily((Type type) => type == typeof(IProjectionTenantBoundary)
            ? boundary : ((IServiceProvider)inner).GetService(type));
        var services = new ServiceCollection();
        services.AddSingleton(store);
        var handler = new TenantBoundaryTestHandler();
        services.AddSingleton(handler);
        await using var provider = services.BuildServiceProvider();
        var registry = new InMemoryProjectionRegistry();
        var builder = new ProjectionBuilder<TenantBoundaryTestRow>(registry);
        var invocations = 0;
        builder.Async();
        if (path == 2)
        {
            builder.WhenHandledBy<TestOrderPlaced, TenantBoundaryTestHandler>();
        }
        else
        {
            builder.When<TestOrderPlaced>((row, _) => { invocations++; row.Amount++; });
            if (path == 1) { builder.KeyedBy<TestOrderPlaced>(_ => "row"); }
        }
        builder.Build();
        var registration = registry.GetRegistration(typeof(TenantBoundaryTestRow)).ShouldNotBeNull();
        ProjectionEvent[] events = [new(new TestOrderPlaced(), "row", 1, partition), new(new TestOrderPlaced(), "row", 2, source)];
        var context = new EventNotificationContext("row", "Order", 1, DateTimeOffset.UtcNow, GlobalPosition: 2);
        if (succeeds)
        {
            await registration.InlineApply!(events, context, provider, CancellationToken.None);
            (invocations + handler.Calls).ShouldBe(2);
            (await inner.GetByIdAsync("row", CancellationToken.None)).ShouldNotBeNull().Amount.ShouldBe(2);
        }
        else
        {
            await Should.ThrowAsync<InvalidOperationException>(() => registration.InlineApply!(events, context, provider, CancellationToken.None));
            (invocations + handler.Calls).ShouldBe(0);
            (await inner.GetByIdAsync("row", CancellationToken.None)).ShouldBeNull();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefuseWritesWhenAHandlerChangesTheBoundary(bool replay)
    {
        IProjectionStore<TenantBoundaryTestRow> inner = new InMemoryProjectionStore<TenantBoundaryTestRow>();
        var boundary = ProjectionTenantBoundary.Partition("A");
        var store = A.Fake<IProjectionStore<TenantBoundaryTestRow>>();
        A.CallTo(() => store.GetService(A<Type>._)).ReturnsLazily((Type type) => type == typeof(IProjectionTenantBoundary)
            ? boundary : inner.GetService(type));
        var services = new ServiceCollection();
        services.AddSingleton(store);
        await using var provider = services.BuildServiceProvider();
        var registry = new InMemoryProjectionRegistry();
        var builder = new ProjectionBuilder<TenantBoundaryTestRow>(registry);
        builder.Async().When<TestOrderPlaced>((row, _) =>
        {
            row.Amount++;
            boundary = ProjectionTenantBoundary.Partition("B");
        });
        builder.Build();
        var registration = registry.GetRegistration(typeof(TenantBoundaryTestRow)).ShouldNotBeNull();
        if (replay)
        {
            var positioned = inner.GetService(typeof(IPositionedProjectionStore<TenantBoundaryTestRow>))
                .ShouldBeAssignableTo<IPositionedProjectionStore<TenantBoundaryTestRow>>();
            var fold = new ProjectionReplayFold<TenantBoundaryTestRow>(
                (MultiStreamProjection<TenantBoundaryTestRow>)registration.Projection, store, positioned, provider);
            await fold.FoldAsync(new TestOrderPlaced(), "row", "Order", 1, DateTimeOffset.UtcNow, 1, CancellationToken.None, "A");
            await Should.ThrowAsync<InvalidOperationException>(() => fold.FlushAsync(null, null, "orders", CancellationToken.None));
        }
        else
        {
            ProjectionEvent[] events = [new(new TestOrderPlaced(), "row", 1, "A")];
            var context = new EventNotificationContext("row", "Order", 1, DateTimeOffset.UtcNow, GlobalPosition: 1);
            await Should.ThrowAsync<InvalidOperationException>(() => registration.InlineApply!(events, context, provider, CancellationToken.None));
        }
        (await inner.GetByIdAsync("row", CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public void DistinguishUnknownBoundaryAndAnExplicitEstateWideDestination()
    {
        var unknown = A.Fake<IProjectionStore<TenantBoundaryTestRow>>();
        Should.Throw<InvalidOperationException>(() => ProjectionTenantValidation.Read(unknown));
        ProjectionTenantValidation.Read(new InMemoryProjectionStore<TenantBoundaryTestRow>()).ShouldBe(ProjectionTenantBoundary.EstateWide);
    }

    [Fact]
    public void BindDifferentTenantPartitionsToDifferentStorageScopes()
    {
        var a = ProjectionTenantValidation.Scope(ProjectionTenantBoundary.Partition("Acme"), "sqlserver/host/db");
        var b = ProjectionTenantValidation.Scope(ProjectionTenantBoundary.Partition("acme"), "sqlserver/host/db");
        a.ShouldNotBe(b);
        ProjectionTenantValidation.Scope(ProjectionTenantBoundary.Partition("Acme"), "sqlserver/host/db").ShouldBe(a);
    }

    [Fact]
    public void PreserveOpaqueTenantIdentityWithoutExceedingTheDurableScopeWidth()
    {
        string[] tenants = [new('\u754C', 64), "\uD800", "\uD801", "\uFFFD", "\U0001F600", "\\U0001F600", "a/b", "a%2Fb", "a:b", "a", "ab"];
        var scopes = tenants.Select(tenant => ProjectionTenantValidation.Scope(
            ProjectionTenantBoundary.Partition(tenant), new string('c', 512))).ToArray();
        scopes.Distinct(StringComparer.Ordinal).Count().ShouldBe(tenants.Length);
        scopes.ShouldAllBe(scope => scope != null && scope.Length <= 512);
        ProjectionTenantValidation.Scope(ProjectionTenantBoundary.Partition("a"), "bc")
            .ShouldNotBe(ProjectionTenantValidation.Scope(ProjectionTenantBoundary.Partition("ab"), "c"));
        ProjectionTenantValidation.Scope(ProjectionTenantBoundary.EstateWide, new string('c', 512))!.Length.ShouldBeLessThanOrEqualTo(512);
        ProjectionTenantValidation.Scope(ProjectionTenantBoundary.EstateWide, new string('c', 505))!.Length.ShouldBe(512);
        ProjectionTenantValidation.Scope(ProjectionTenantBoundary.EstateWide, new string('c', 506))!.Length.ShouldBeLessThan(512);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void RefuseAConfinedDestinationWithoutAnActualStorageScope(string? cluster)
    {
        foreach (var tenant in new[] { "A", "B", "__untenanted__" })
        {
            Should.Throw<InvalidOperationException>(() =>
                ProjectionTenantValidation.Scope(ProjectionTenantBoundary.Partition(tenant), cluster));
        }
        ProjectionTenantValidation.Scope(ProjectionTenantBoundary.EstateWide, cluster).ShouldBeNull();
    }

    [Fact]
    public void RefuseAnUnmarkedProjectionStoreWhenRowDiscriminatorTenancyIsEnabled()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IProjectionStore<TenantBoundaryTestRow>>(new InMemoryProjectionStore<TenantBoundaryTestRow>());
        var error = Should.Throw<InvalidOperationException>(() => services.AddMultiTenancy(
            options => options.Strategy = Excalibur.MultiTenancy.TenantIsolationStrategy.RowDiscriminator));
        error.Message.ShouldContain("IProjectionStore");
    }

    [Fact]
    public void RefuseToUpgradeAnEstateWideStoreByWrappingItInATenantGuard()
    {
        var tenant = A.Fake<ITenantContext>();
        A.CallTo(() => tenant.TenantId).Returns("Acme");
        A.CallTo(() => tenant.HasTenant).Returns(true);
        var guarded = new TenantScopedProjectionStore<TenantBoundaryTestRow>(new InMemoryProjectionStore<TenantBoundaryTestRow>(), tenant);
        Should.Throw<InvalidOperationException>(() => ProjectionTenantValidation.Read(guarded));
    }
}

public sealed class TenantBoundaryTestRow
{
    public decimal Amount { get; set; }
}

internal sealed class TenantBoundaryTestHandler : IProjectionEventHandler<TenantBoundaryTestRow, TestOrderPlaced>
{
    internal int Calls { get; private set; }

    public Task HandleAsync(TenantBoundaryTestRow projection, TestOrderPlaced @event,
        ProjectionHandlerContext context, CancellationToken cancellationToken)
    {
        Calls++;
        projection.Amount++;
        return Task.CompletedTask;
    }
}
