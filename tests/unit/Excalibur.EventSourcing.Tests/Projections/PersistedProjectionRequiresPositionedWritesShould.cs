// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Regression")]
public sealed class PersistedProjectionRequiresPositionedWritesShould
{
	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public async Task RefuseEveryApplyFactoryBeforeHandlersWhenCapabilityIsAbsent(int factory)
	{
		var calls = new Calls();
		var services = new ServiceCollection();
		services.AddSingleton<IProjectionStore<Counter>>(new PlainStore());
		var apply = Build(factory, services, calls);
		using var provider = services.BuildServiceProvider();

		var exception = await Should.ThrowAsync<InvalidOperationException>(() => apply(
			[new ProjectionEvent(new Counted(), "aggregate", 1)], Context(), provider, CancellationToken.None));

		exception.Message.ShouldContain("positioned compare-and-set");
		calls.Count.ShouldBe(0);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public async Task RefuseTheWholePageBeforeAnyHandlerWhenALaterEventIsUnpositioned(int factory)
	{
		var calls = new Calls();
		var services = new ServiceCollection();
		services.AddSingleton<IProjectionStore<Counter>>(new InMemoryProjectionStore<Counter>());
		var apply = Build(factory, services, calls);
		using var provider = services.BuildServiceProvider();

		await Should.ThrowAsync<InvalidOperationException>(() => apply(
			[new ProjectionEvent(new Counted(), "aggregate", 1), new ProjectionEvent(new Counted(), "aggregate", null)],
			Context(), provider, CancellationToken.None));

		calls.Count.ShouldBe(0);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public async Task StillApplyACommittedPageThroughEveryFactory(int factory)
	{
		var calls = new Calls();
		var services = new ServiceCollection();
		var store = new InMemoryProjectionStore<Counter>();
		services.AddSingleton<IProjectionStore<Counter>>(store);
		var apply = Build(factory, services, calls);
		using var provider = services.BuildServiceProvider();

		await apply([new ProjectionEvent(new Counted(), "aggregate", 1)], Context(), provider, CancellationToken.None);

		calls.Count.ShouldBe(1);
		var row = await store.GetWithPositionAsync(factory == 1 ? "shared" : "aggregate", CancellationToken.None);
		row.Projection.ShouldNotBeNull();
		row.Projection.Total.ShouldBe(1);
		row.Position.Value.ShouldBe(1);
	}

	[Fact]
	public void ProbeTheSameExactTypeAndResolvedCapabilityAsTheApplyPath()
	{
		var services = new ServiceCollection();
		services.AddSingleton<IProjectionStore<Counter>>(new InMemoryProjectionStore<Counter>());
		using var provider = services.BuildServiceProvider();
		var probe = new PositionedProjectionProbe<Counter>();
		probe.ProjectionType.ShouldBe(typeof(Counter));
		probe.IsPositioned(provider).ShouldBeTrue();
	}

	private static ProjectionRegistration.InlineApplyDelegate Build(int factory, IServiceCollection services, Calls calls)
	{
		var registry = new InMemoryProjectionRegistry();
		if (factory == 3)
		{
			var builder = new ImmutableProjectionBuilder<Counter>(services);
			builder.WhenCreating<Counted>(_ => { calls.Count++; return new Counter { Total = 1 }; });
			builder.Build(registry);
		}
		else
		{
			var builder = new ProjectionBuilder<Counter>(services);
			if (factory == 2)
			{
				services.AddSingleton(new CountingHandler(calls));
				builder.WhenHandledBy<Counted, CountingHandler>();
			}
			else
			{
				builder.When<Counted>((state, _) => { calls.Count++; state.Total++; });
				if (factory == 1)
				{
					builder.KeyedBy<Counted>(static _ => "shared");
				}
			}
			builder.Build(registry);
		}
		return registry.GetRegistration(typeof(Counter))!.InlineApply!;
	}

	private static EventNotificationContext Context() => new("aggregate", "Counter", 1, DateTimeOffset.UnixEpoch);

	private sealed class PlainStore : IProjectionStore<Counter>
	{
		public object? GetService(Type serviceType) => null;
		public Task<Counter?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
			throw new InvalidOperationException("Unexpected storage access before capability validation.");
		public Task UpsertAsync(string id, Counter projection, CancellationToken cancellationToken) =>
			throw new InvalidOperationException("Unexpected storage access before capability validation.");
		public Task DeleteAsync(string id, CancellationToken cancellationToken) =>
			throw new InvalidOperationException("Unexpected storage access before capability validation.");
		public Task<IReadOnlyList<Counter>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) =>
			throw new InvalidOperationException("Unexpected storage access before capability validation.");
		public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) =>
			throw new InvalidOperationException("Unexpected storage access before capability validation.");
	}

	private sealed class Calls
	{
		public int Count { get; set; }
	}

	private sealed class Counter
	{
		public int Total { get; set; }
	}

	private sealed class CountingHandler(Calls calls) : IProjectionEventHandler<Counter, Counted>
	{
		public Task HandleAsync(Counter projection, Counted @event, ProjectionHandlerContext context, CancellationToken cancellationToken)
		{
			calls.Count++;
			projection.Total++;
			return Task.CompletedTask;
		}
	}

	[MessageName("Test.StrictPositioned.Counted")]
	private sealed record Counted : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString("N");
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UnixEpoch;
		public string EventType { get; init; } = nameof(Counted);
		public IDictionary<string, object>? Metadata { get; init; }
	}
}
