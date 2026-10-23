// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.InMemory;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Subscriptions;
using Excalibur.EventSourcing.Tests.Projections;

using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// The two ways to register projection processing, and what separates them.
/// </summary>
/// <remarks>
/// <para>
/// A host that cannot run a <see cref="BackgroundService"/>, or one that wants its read models current
/// by the time a request returns, needs the processor WITHOUT a background worker. Before the split
/// there was no such registration: asking for the processor started a worker as well, so the two
/// scenarios were unreachable.
/// </para>
/// <para>
/// <b>These arms resolve through a real container.</b> A registration asserted by reading the
/// descriptor list proves the descriptor exists, never that the container can satisfy it — and the
/// processor's own dependency graph is what used to be missing.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ProjectionProcessorRegistrationShould
{
	[Fact]
	public void ResolveTheProcessorWithoutRegisteringABackgroundWorker()
	{
		// SAFETY: no worker. This is the whole point of the registration -- an IIS-hosted API that drives
		// projections itself must not also start a poller, and a Function host cannot run one at all.
		using var provider = Build(es => es.AddProjectionProcessor());

		_ = provider.GetRequiredService<IProjectionProcessor>();

		provider.GetServices<IHostedService>()
			.ShouldNotContain(
				service => service is AsyncProjectionProcessingHostedService,
				"AddProjectionProcessor must not start a background worker");
	}

	[Fact]
	public void RegisterBothTheProcessorAndTheWorkerWhenProcessingIsEnabled()
	{
		// LIVENESS, and the arm that stops the one above being satisfied by a registration that does
		// nothing at all. The long-running path must still get its worker.
		using var provider = Build(es => es.EnableProjectionProcessing());

		_ = provider.GetRequiredService<IProjectionProcessor>();

		provider.GetServices<IHostedService>()
			.ShouldContain(
				service => service is AsyncProjectionProcessingHostedService,
				"EnableProjectionProcessing must still start the background worker");
	}

	[Fact]
	public void ShareOneCursorWhenBothAreCalled()
	{
		// Nothing stops a consumer calling both, and two independent cursors over one subscription is
		// the defect that shape invites. The worker and the pull API must be the SAME instance, so a
		// manual drive and a background pass serialise against each other instead of racing.
		using var provider = Build(es =>
		{
			_ = es.AddProjectionProcessor();
			_ = es.EnableProjectionProcessing();
		});

		var processor = provider.GetRequiredService<IProjectionProcessor>();
		var host = provider.GetRequiredService<AsyncProjectionProcessingHost>();

		processor.ShouldBeSameAs(host);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	[Trait("Pattern", "Regression")]
	public async Task InitializeRegisteredMutableAndImmutableProjectionsBeforeAnyLocalSave(bool registerWorker)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddInMemoryProjectionStore<OrderSummary>();
		_ = services.AddInMemoryProjectionStore<OrderRecord>();
		_ = services.AddExcalibur(x => x.AddEventSourcing(es =>
		{
			_ = es.UseInMemory();
			_ = es.RegisterEventTypes(typeof(TestOrderPlaced), typeof(TestOrderShipped));
			_ = es.AddProjection<OrderSummary>(p => p.Async()
				.When<TestOrderPlaced>((state, e) => { state.Total = e.Amount; state.EventCount++; })
				.When<TestOrderShipped>((state, e) => { state.ShippedAt = e.ShippedAt; state.EventCount++; }));
			_ = es.AddImmutableProjection<OrderRecord>(p => p.Async()
				.WhenCreating<TestOrderPlaced>(e => new OrderRecord(e.Amount, null))
				.WhenTransforming<TestOrderShipped>((state, e) => state with { ShippedAt = e.ShippedAt }));
			if (registerWorker)
			{
				_ = es.EnableProjectionProcessing(options => options.SubscriptionScope = "processor-registration/events/views");
			}
			else
			{
				_ = es.AddProjectionProcessor(options => options.SubscriptionScope = "processor-registration/events/views");
			}
		}));
		await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
		{
			ValidateOnBuild = true,
			ValidateScopes = true,
		});

		// Resolve the driver first. A projection-only process never constructs a repository or the
		// notification broker; either one would hide missing initialization on this registration path.
		var processor = provider.GetRequiredService<IProjectionProcessor>();
		var eventStore = provider.GetRequiredService<IEventStore>();
		var shippedAt = DateTimeOffset.UtcNow;
		var appended = await eventStore.AppendAsync("order-1", "Order",
			[new TestOrderPlaced { Amount = 42m }, new TestOrderShipped { ShippedAt = shippedAt }],
			-1, TestContext.Current.CancellationToken);
		appended.Success.ShouldBeTrue();

		var processed = await processor.ProcessPendingAsync(TestContext.Current.CancellationToken);

		processed.Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);
		processed.EventsApplied.ShouldBe(2);
		var mutable = await provider.GetRequiredService<IProjectionStore<OrderSummary>>()
			.GetByIdAsync("order-1", TestContext.Current.CancellationToken);
		mutable.ShouldNotBeNull().Total.ShouldBe(42m);
		mutable.EventCount.ShouldBe(2);
		mutable.ShippedAt.ShouldBe(shippedAt);
		var immutable = await provider.GetRequiredService<IProjectionStore<OrderRecord>>()
			.GetByIdAsync("order-1", TestContext.Current.CancellationToken);
		immutable.ShouldNotBeNull().Total.ShouldBe(42m);
		immutable.ShippedAt.ShouldBe(shippedAt);
		var checkpoint = await provider.GetRequiredService<ISubscriptionCheckpointStore>()
			.GetCheckpointAsync(GlobalStreamProjectionOptions.DefaultProjectionName, TestContext.Current.CancellationToken);
		checkpoint.ShouldBe(appended.LastEventPosition);
		checkpoint.ShouldNotBeNull();
	}

	private static ServiceProvider Build(Action<IEventSourcingBuilder> configure)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddExcalibur(x => x.AddEventSourcing(es =>
		{
			// A projection, because that is what a consumer registering processing actually has, and
			// because AddProjection is what brings in the registry the host depends on.
			_ = es.AddProjection<OrderSummary>(p => p
				.Async()
				.When<TestOrderPlaced>((projection, @event) => projection.EventCount++));
			configure(es);
		}));

		// validateOnBuild, deliberately: it walks every implementation-type descriptor, so a processor
		// whose dependency graph cannot be satisfied fails HERE rather than on the first trigger in
		// production -- which is the failure mode this registration previously had.
		return services.BuildServiceProvider(new ServiceProviderOptions
		{
			ValidateOnBuild = true,
			ValidateScopes = true,
		});
	}
}
