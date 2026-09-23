// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Implementation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Handlers.Tests;

/// <summary>
/// A handler registered by <c>AddAggregateHandler</c> must be reachable through the DISPATCHER, not
/// merely resolvable from the container.
/// </summary>
/// <remarks>
/// <para>
/// The sibling suites resolve <c>IActionHandler&lt;T&gt;</c> and call <c>HandleAsync</c> directly. That
/// proves the handler's logic and is silent on the question that decides whether the feature works at
/// all: whether the handler index ever saw the registration. The index is built from a descriptor's
/// implementation type and instance, so a handler registered through a factory closure contributes no
/// entry and the dispatcher cannot find it — while a hand-resolved call succeeds and every such test
/// passes.
/// </para>
/// <para>
/// These arms therefore go through <see cref="IDispatcher"/>. The first asserts the property directly at
/// the registration; the second asserts the behaviour end-to-end over a real event store.
/// </para>
/// </remarks>
public sealed class AggregateHandlerIsReachableByDispatchShould
{
	[Fact]
	public void RegisterTheHandlerInAFormTheIndexCanSee()
	{
		// The index reads ImplementationType and ImplementationInstance. A factory registration answers
		// null to both, which is the whole defect, so this asserts the descriptor shape the index needs
		// rather than waiting for the symptom.
		var services = new ServiceCollection();
		_ = services.AddAggregateHandler<Gadget, string, RenameGadget>(
			resolveId: msg => msg.GadgetId,
			decide: (gadget, msg, _) =>
			{
				gadget.Rename(msg.NewName);
				return Task.CompletedTask;
			});

		var descriptor = services.Single(d => d.ServiceType == typeof(IActionHandler<RenameGadget>));

		descriptor.ImplementationType.ShouldNotBeNull(
			"the handler index is built from ImplementationType; a factory registration leaves it null and "
			+ "the handler is never indexed, so the dispatcher cannot find it");
		descriptor.ImplementationType.ShouldBe(typeof(AggregateHandler<Gadget, string, RenameGadget>));
	}

	[Fact]
	public async Task BeInvokedWhenItsMessageIsDispatched()
	{
		// The requirement, not a proxy for it: dispatch the message and assert the aggregate changed.
		var store = NewRealEventStore();
		var id = Guid.NewGuid().ToString();

		var seedRepo = NewRepository(store);
		var seed = new Gadget(id);
		seed.Create(id);
		await seedRepo.SaveAsync(seed, expectedETag: null, CancellationToken.None);

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IEventSourcedRepository<Gadget, string>>(NewRepository(store));
		_ = services.AddAggregateHandler<Gadget, string, RenameGadget>(
			resolveId: msg => msg.GadgetId,
			decide: (gadget, msg, _) =>
			{
				gadget.Rename(msg.NewName);
				return Task.CompletedTask;
			});
		_ = services.AddDispatch(_ => { });

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		_ = await dispatcher.DispatchAsync(new RenameGadget(id, "Dispatched"), CancellationToken.None);

		// Reload on a FRESH repository over the same store: if the handler never ran, this is unchanged
		// and nothing anywhere reported that the message went nowhere.
		var reloaded = await NewRepository(store).GetByIdAsync(id, CancellationToken.None);
		reloaded.ShouldNotBeNull();
		reloaded!.Name.ShouldBe("Dispatched",
			"the aggregate handler registered by AddAggregateHandler was not reached by the dispatcher");
	}

	private static IEventStore NewRealEventStore()
		=> new ServiceCollection()
			.AddInMemoryEventStore()
			.BuildServiceProvider()
			.GetRequiredKeyedService<IEventStore>("default");

	private static EventSourcedRepository<Gadget, string> NewRepository(IEventStore store)
		=> new(
			store,
			new JsonEventSerializer(
				new ServiceCollection().AddEventTypes(typeof(GadgetCreated), typeof(GadgetRenamed))
					.BuildServiceProvider().GetRequiredService<IEventTypeRegistry>(),
				options: null,
				allowAssemblyScan: false),
			id => new Gadget(id),
			Options.Create(new EventSourcedRepositoryOptions()));

	#region Real test aggregate + message

	internal sealed class Gadget : AggregateRoot, IAggregateSnapshotSupport
	{
		public Gadget()
		{
		}

		public Gadget(string id) : base(id)
		{
		}

		public string Name { get; private set; } = string.Empty;

		public void Create(string id) => RaiseEvent(new GadgetCreated(id));

		public void Rename(string name) => RaiseEvent(new GadgetRenamed(Id, name));

		protected override bool ApplyEventInternal(IDomainEvent @event) => @event switch
		{
			GadgetCreated e => Apply(e),
			GadgetRenamed e => Apply(e),
			_ => throw new InvalidOperationException($"Unknown event {@event.GetType().Name}"),
		};

		private bool Apply(GadgetCreated e)
		{
			Id = e.AggregateId;
			return true;
		}

		private bool Apply(GadgetRenamed e)
		{
			Name = e.Name;
			return true;
		}
	}

	[MessageName("Test.GadgetCreated")]
	internal sealed record GadgetCreated(string AggregateId) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public IDictionary<string, object>? Metadata { get; init; }
	}

	[MessageName("Test.GadgetRenamed")]
	internal sealed record GadgetRenamed(string AggregateId, string Name) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public IDictionary<string, object>? Metadata { get; init; }
	}

	internal sealed record RenameGadget(string GadgetId, string NewName) : IDispatchAction;

	#endregion Real test aggregate + message
}
