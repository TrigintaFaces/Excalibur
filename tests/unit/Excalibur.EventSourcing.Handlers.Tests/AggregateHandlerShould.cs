// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Implementation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Handlers.Tests;

/// <summary>
/// Real-infra conformance lock for the aggregate-handler Decider (bead C1 / 0aajc0).
/// Exercises <c>AddAggregateHandler</c> through a REAL <see cref="EventSourcedRepository{TAggregate, TKey}"/>
/// over a REAL in-process event store (no mocks): the emitted behavior is asserted by reloading the
/// aggregate on a fresh repository over the same store.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class AggregateHandlerShould
{
	// A real event store, obtained through the production DI registration (InMemoryEventStore is
	// internal, so it is resolved via AddInMemoryEventStore rather than constructed directly).
	private static IEventStore NewRealEventStore()
		=> new ServiceCollection()
			.AddInMemoryEventStore()
			.BuildServiceProvider()
			.GetRequiredKeyedService<IEventStore>("default");

	private static EventSourcedRepository<Widget, string> NewRepository(IEventStore store)
		=> new(
			store,
			new JsonEventSerializer(
				// A declared name resolves through the registry, never through the assembly scan --
				// the scan matches CLR type names and a declared name deliberately is not one.
				new ServiceCollection().AddEventTypes(typeof(WidgetCreated), typeof(WidgetRenamed))
					.BuildServiceProvider().GetRequiredService<IEventTypeRegistry>(),
				options: null,
				allowAssemblyScan: false),
			id => new Widget(id),
			Options.Create(new EventSourcedRepositoryOptions()));

	private static ServiceProvider BuildHandlerProvider(
		EventSourcedRepository<Widget, string> repository,
		Func<Widget, PromoteWidget, CancellationToken, Task> decide)
	{
		var services = new ServiceCollection();
		services.AddSingleton<IEventSourcedRepository<Widget, string>>(repository);
		services.AddAggregateHandler<Widget, string, PromoteWidget>(
			resolveId: msg => msg.WidgetId,
			decide: decide);
		return services.BuildServiceProvider();
	}

	[Fact]
	public async Task ApplyTheDecisionAndPersistItThroughTheRealRepository()
	{
		var store = NewRealEventStore();
		var id = Guid.NewGuid().ToString();

		// Seed v1 through a real repository.
		var seedRepo = NewRepository(store);
		var seed = new Widget(id);
		seed.Create(id);
		await seedRepo.SaveAsync(seed, expectedETag: null, CancellationToken.None);

		// Dispatch a message through the handler over the SAME store.
		var handlerRepo = NewRepository(store);
		await using var provider = BuildHandlerProvider(
			handlerRepo,
			(widget, msg, _) =>
			{
				widget.Rename(msg.NewName);
				return Task.CompletedTask;
			});
		var handler = provider.GetRequiredService<IActionHandler<PromoteWidget>>();

		await handler.HandleAsync(new PromoteWidget(id, "Renamed"), CancellationToken.None);

		// Reload on a FRESH repository over the same store: the decision must be durably persisted.
		var reloadRepo = NewRepository(store);
		var reloaded = await reloadRepo.GetByIdAsync(id, CancellationToken.None);
		reloaded.ShouldNotBeNull();
		reloaded!.Name.ShouldBe("Renamed");
		reloaded.Version.ShouldBeGreaterThan(seed.Version);
	}

	[Fact]
	public async Task ThrowResourceNotFoundWhenTheAggregateDoesNotExistAndWriteNothing()
	{
		var store = NewRealEventStore();
		var missingId = Guid.NewGuid().ToString();

		var handlerRepo = NewRepository(store);
		await using var provider = BuildHandlerProvider(
			handlerRepo,
			(widget, msg, _) =>
			{
				widget.Rename(msg.NewName);
				return Task.CompletedTask;
			});
		var handler = provider.GetRequiredService<IActionHandler<PromoteWidget>>();

		_ = await Should.ThrowAsync<ResourceNotFoundException>(
			() => handler.HandleAsync(new PromoteWidget(missingId, "Renamed"), CancellationToken.None));

		// Honest not-found: no aggregate was fabricated / persisted.
		var reloadRepo = NewRepository(store);
		(await reloadRepo.GetByIdAsync(missingId, CancellationToken.None)).ShouldBeNull();
	}

	[Fact]
	public async Task ThrowConcurrencyExceptionWhenTheAggregateIsAdvancedOutOfBand()
	{
		var store = NewRealEventStore();
		var id = Guid.NewGuid().ToString();

		var seedRepo = NewRepository(store);
		var seed = new Widget(id);
		seed.Create(id);
		await seedRepo.SaveAsync(seed, expectedETag: null, CancellationToken.None);

		var handlerRepo = NewRepository(store);
		// The decision advances the aggregate OUT OF BAND (a concurrent writer over the same store)
		// after the handler captured the load-time ETag — so the handler's optimistic save is stale.
		await using var provider = BuildHandlerProvider(
			handlerRepo,
			async (widget, msg, ct) =>
			{
				var concurrent = NewRepository(store);
				var other = await concurrent.GetByIdAsync(widget.Id, ct);
				other!.Rename("concurrent-winner");
				await concurrent.SaveAsync(other, other.ETag, ct);

				widget.Rename(msg.NewName);
			});
		var handler = provider.GetRequiredService<IActionHandler<PromoteWidget>>();

		_ = await Should.ThrowAsync<ConcurrencyException>(
			() => handler.HandleAsync(new PromoteWidget(id, "loser"), CancellationToken.None));
	}

	#region Real test aggregate + message

	/// <summary>A minimal real event-sourced aggregate with a domain method that raises an event.</summary>
	internal sealed class Widget : AggregateRoot, IAggregateSnapshotSupport
	{
		public Widget()
		{
		}

		public Widget(string id) : base(id)
		{
		}

		public string Name { get; private set; } = string.Empty;

		public void Create(string id) => RaiseEvent(new WidgetCreated(id));

		public void Rename(string name) => RaiseEvent(new WidgetRenamed(Id, name));

		protected override bool ApplyEventInternal(IDomainEvent @event) => @event switch
		{
			WidgetCreated e => Apply(e),
			WidgetRenamed e => Apply(e),
			_ => throw new InvalidOperationException($"Unknown event {@event.GetType().Name}"),
		};

		private bool Apply(WidgetCreated e)
		{
			Id = e.AggregateId;
			return true;
		}

		private bool Apply(WidgetRenamed e)
		{
			Name = e.Name;
			return true;
		}
	}

	[MessageName("Test.WidgetCreated")]
	private sealed record WidgetCreated(string AggregateId) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public IDictionary<string, object>? Metadata { get; init; }
	}

	[MessageName("Test.WidgetRenamed")]
	private sealed record WidgetRenamed(string AggregateId, string Name) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public IDictionary<string, object>? Metadata { get; init; }
	}

	/// <summary>The dispatched command routed to the aggregate.</summary>
	internal sealed record PromoteWidget(string WidgetId, string NewName) : IDispatchAction;

	#endregion Real test aggregate + message
}
