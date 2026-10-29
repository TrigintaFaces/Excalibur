// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Projections;
using Excalibur.Dispatch;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "EventSourcing")]
public sealed class ProjectionRetirementShould
{
	[Fact]
	public void PreserveRetirementThroughTheSharedStorageEncoding()
	{
		Assert.Equal(ProjectionPositionKind.Retired, ProjectionPosition.Retired.Kind);
		Assert.Equal(ProjectionPosition.Retired, ProjectionPosition.FromStored(ProjectionPosition.Retired.ToStored()));
		Assert.Null(ProjectionPosition.Retired.ExpectedPositionOrNull);
		Assert.Throws<InvalidOperationException>(() => ProjectionPosition.Retired.Value);
		Assert.NotEqual(ProjectionPosition.Unplaceable, ProjectionPosition.Retired);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task PreventEveryWriteFromRecreatingADeletedKey(bool initiallyPresent)
	{
		var store = new InMemoryProjectionStore<Document>();
		var value = new Document { Count = 1 };
		if (initiallyPresent)
		{
			Assert.Equal(ProjectionAdvanceOutcome.Applied,
				(await store.UpsertAtPositionAsync("key", value, null, 1, CancellationToken.None)).Outcome);
		}

		await store.DeleteAsync("key", CancellationToken.None);
		await store.DeleteAsync("key", CancellationToken.None);
		var observed = await store.GetWithPositionAsync("key", CancellationToken.None);
		Assert.Null(observed.Projection);
		Assert.Equal(ProjectionPosition.Retired, observed.Position);
		Assert.Null(await store.GetByIdAsync("key", CancellationToken.None));
		Assert.Empty(await store.QueryAsync(null, null, CancellationToken.None));
		Assert.Equal(0, await store.CountAsync(null, CancellationToken.None));
		await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpsertAsync("key", value, CancellationToken.None));
		await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpsertUnnumberedAsync("key", value, CancellationToken.None));
		Assert.Equal(ProjectionAdvanceOutcome.Vanished,
			(await store.UpsertAtPositionAsync("key", value, null, 2, CancellationToken.None)).Outcome);
		Assert.Equal(ProjectionAdvanceOutcome.Vanished,
			(await store.UpsertAtPositionAsync("key", value, 1, 2, CancellationToken.None)).Outcome);
		Assert.Equal(ProjectionRefoldOutcome.Vanished,
			(await store.RefoldAtPositionAsync("key", value, 1, CancellationToken.None)).Outcome);
		Assert.Equal(ProjectionRebuildOutcome.Vanished,
			(await store.RebuildAtPositionAsync("key", value, 2, CancellationToken.None)).Outcome);
		Assert.Equal(ProjectionPosition.Retired, (await store.GetWithPositionAsync("key", CancellationToken.None)).Position);
	}

	[Fact]
	public async Task KeepAnIndependentDestinationWritable()
	{
		var oldGeneration = new InMemoryProjectionStore<Document>();
		var newGeneration = new InMemoryProjectionStore<Document>();
		await oldGeneration.DeleteAsync("key", CancellationToken.None);
		Assert.Equal(ProjectionAdvanceOutcome.Applied,
			(await newGeneration.UpsertAtPositionAsync("key", new Document { Count = 2 }, null, 2, CancellationToken.None)).Outcome);
		Assert.Equal(2, (await newGeneration.GetByIdAsync("key", CancellationToken.None))!.Count);
		Assert.Null(await oldGeneration.GetByIdAsync("key", CancellationToken.None));
	}

	[Fact]
	public async Task RetainDeletionWhenTheFirstInsertRacesIt()
	{
		var store = new InMemoryProjectionStore<Document>();
		for (var i = 0; i < 50; i++)
		{
			var key = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
			await Task.WhenAll(
				Task.Run(() => store.DeleteAsync(key, CancellationToken.None)),
				Task.Run(() => store.UpsertAtPositionAsync(key, new Document { Count = 1 }, null, 1, CancellationToken.None)));
			Assert.Null(await store.GetByIdAsync(key, CancellationToken.None));
			Assert.Equal(ProjectionPosition.Retired, (await store.GetWithPositionAsync(key, CancellationToken.None)).Position);
		}
	}

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task KeepRetirementLocalToThePrimaryOrOverrideKey(bool retirePrimary, bool retireOverride)
    {
        var store = new InMemoryProjectionStore<Document>();
        if (retirePrimary) { await store.DeleteAsync("primary", CancellationToken.None); }
        if (retireOverride) { await store.DeleteAsync("shared", CancellationToken.None); }
        var services = new ServiceCollection();
        services.AddSingleton<IProjectionStore<Document>>(store);
        services.AddTransient<Route>();
        using var provider = services.BuildServiceProvider();
        var builder = new ProjectionBuilder<Document>(services);
        builder.WhenHandledBy<Counted, Route>();
        var registry = new InMemoryProjectionRegistry();
        builder.Build(registry);
        var apply = registry.GetRegistration(typeof(Document))!.InlineApply!;
        var context = new EventNotificationContext("primary", "Aggregate", 1, DateTimeOffset.UnixEpoch);
        IReadOnlyList<ProjectionEvent> events = [new(new Counted(), "primary", 1), new(new Counted(), "primary", 2)];
        await apply(events, context, provider, CancellationToken.None);
        await apply(events, context, provider, CancellationToken.None);
        foreach (var (id, retired) in new[] { ("primary", retirePrimary), ("shared", retireOverride) })
        {
            var row = await store.GetWithPositionAsync(id, CancellationToken.None);
            if (retired)
            {
                Assert.Null(row.Projection);
                Assert.Equal(ProjectionPosition.Retired, row.Position);
            }
            else
            {
                Assert.Equal(2, row.Projection!.Count);
                Assert.Equal(ProjectionPosition.At(2), row.Position);
            }
        }
    }

    [Fact]
    public async Task SettleSynchronousDeliveryAfterDeletionWithoutRecreatingState()
    {
        var store = new InMemoryProjectionStore<Document>();
        var services = new ServiceCollection();
        services.AddSingleton<IProjectionStore<Document>>(store);
        using var provider = services.BuildServiceProvider();
        var builder = new ProjectionBuilder<Document>(services);
        builder.When<Counted>(static (state, _) => state.Count++);
        var registry = new InMemoryProjectionRegistry();
        builder.Build(registry);
        var apply = registry.GetRegistration(typeof(Document))!.InlineApply!;
        var context = new EventNotificationContext("primary", "Aggregate", 1, DateTimeOffset.UnixEpoch);
        await apply([new(new Counted(), "primary", 1)], context, provider, CancellationToken.None);
        await store.DeleteAsync("primary", CancellationToken.None);
        await apply([new(new Counted(), "primary", 2)], context, provider, CancellationToken.None);
        Assert.Equal(ProjectionPosition.Retired,
            (await store.GetWithPositionAsync("primary", CancellationToken.None)).Position);
        Assert.Null(await store.GetByIdAsync("primary", CancellationToken.None));
    }

    [Fact]
    public async Task PreserveRetirementDuringWholeStreamReplay()
    {
        var store = new InMemoryProjectionStore<Document>();
        await store.DeleteAsync("retired", CancellationToken.None);
        var projection = new MultiStreamProjection<Document>();
        projection.AddHandler<Counted>(static (state, _) => state.Count++);
        using var provider = new ServiceCollection().BuildServiceProvider();
        var replay = new ProjectionReplayFold<Document>(projection, store, store, provider);
        await replay.FoldAsync(new Counted(), "retired", "Aggregate", 1, DateTimeOffset.UnixEpoch, 1, CancellationToken.None);
        await replay.FoldAsync(new Counted(), "live", "Aggregate", 1, DateTimeOffset.UnixEpoch, 2, CancellationToken.None);
        await replay.FlushAsync(null, null, "test", CancellationToken.None);
        Assert.Null(await store.GetByIdAsync("retired", CancellationToken.None));
        Assert.Equal(ProjectionPosition.Retired,
            (await store.GetWithPositionAsync("retired", CancellationToken.None)).Position);
        Assert.Equal(1, (await store.GetByIdAsync("live", CancellationToken.None))!.Count);
    }

    [Fact]
    public async Task PreserveRetirementDuringImmutableDelivery()
    {
        var store = new InMemoryProjectionStore<Document>();
        await store.DeleteAsync("retired", CancellationToken.None);
        var services = new ServiceCollection();
        services.AddSingleton<IProjectionStore<Document>>(store);
        using var provider = services.BuildServiceProvider();
        var builder = new ImmutableProjectionBuilder<Document>(services);
        builder.WhenCreating<Counted>(static _ => new Document { Count = 1 });
        var registry = new InMemoryProjectionRegistry();
        builder.Build(registry);
        var apply = registry.GetRegistration(typeof(Document))!.InlineApply!;
        var context = new EventNotificationContext("retired", "Aggregate", 1, DateTimeOffset.UnixEpoch);
        await apply([new(new Counted(), "retired", 1), new(new Counted(), "live", 2)],
            context, provider, CancellationToken.None);
        Assert.Null(await store.GetByIdAsync("retired", CancellationToken.None));
        Assert.Equal(ProjectionPosition.Retired,
            (await store.GetWithPositionAsync("retired", CancellationToken.None)).Position);
        Assert.Equal(1, (await store.GetByIdAsync("live", CancellationToken.None))!.Count);
    }

    private sealed class Route : IProjectionEventHandler<Document, Counted>
    {
        public Task HandleAsync(Document projection, Counted @event, ProjectionHandlerContext context,
            CancellationToken cancellationToken)
        {
            context.OverrideProjectionId = "shared";
            projection.Count++;
            return Task.CompletedTask;
        }
    }

    [MessageName("Test.ProjectionRetirement.Counted")]
    private sealed record Counted : IDomainEvent
    {
        public string EventId { get; init; } = Guid.NewGuid().ToString("N");
        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UnixEpoch;
        public string EventType { get; init; } = nameof(Counted);
        public IDictionary<string, object>? Metadata { get; init; }
    }

	internal sealed class Document
	{
		public int Count { get; set; }
	}
}
