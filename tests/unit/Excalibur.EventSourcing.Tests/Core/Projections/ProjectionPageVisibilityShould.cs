// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;
using Excalibur.EventSourcing.Tests.Projections;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>Exercises the production registration and probe path, rather than a private host hook.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ProjectionPageVisibilityShould
{
	[Fact]
	public async Task FinishSearchVisibilityBeforeAcknowledgingThePage()
	{
		using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var barrier = A.Fake<IProjectionVisibilityBarrier>();
		A.CallTo(() => barrier.EnsureVisibleAsync(A<CancellationToken>._)).ReturnsLazily(async (CancellationToken token) =>
		{
			entered.TrySetResult();
			await release.Task.WaitAsync(token).ConfigureAwait(false);
		});
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		var held = new InMemoryProjectionStore<OrderSummary>();
		await using var provider = Build(new Stream(), held, barrier, checkpoints);
		var host = Driver(provider);
		var processing = host.ProcessPendingAsync(safety.Token);
		await entered.Task.WaitAsync(safety.Token).ConfigureAwait(false);
		try
		{
			(await held.GetByIdAsync("order-1", safety.Token).ConfigureAwait(false)).ShouldNotBeNull().EventCount.ShouldBe(1);
			(await checkpoints.GetCheckpointAsync("visibility-host", AsyncProjectionProcessingHost.ProjectionKeyFor(typeof(OrderSummary)), safety.Token).ConfigureAwait(false)).ShouldBeNull(
				"folded state is not a searchable acknowledgment while its visibility barrier is blocked");
			processing.IsCompleted.ShouldBeFalse();
		}
		finally
		{
			release.TrySetResult();
		}
		(await processing.WaitAsync(safety.Token).ConfigureAwait(false)).Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);
		(await checkpoints.GetCheckpointAsync("visibility-host", AsyncProjectionProcessingHost.ProjectionKeyFor(typeof(OrderSummary)), safety.Token).ConfigureAwait(false)).ShouldBe(1);
	}

	[Fact]
	public async Task RunTheVisibilityBarrierEvenWhenTheProjectionAlreadyContainsTheEvent()
	{
		using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		var held = new InMemoryProjectionStore<OrderSummary>();
		await held.UpsertAtPositionAsync("order-1", new OrderSummary { EventCount = 7 }, null, 1, safety.Token)
			.ConfigureAwait(false);
		var barrier = A.Fake<IProjectionVisibilityBarrier>();
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		await using var provider = Build(new Stream(), held, barrier, checkpoints);
		var result = await Driver(provider).ProcessPendingAsync(safety.Token).ConfigureAwait(false);
		result.Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);
		(await held.GetByIdAsync("order-1", safety.Token).ConfigureAwait(false)).ShouldNotBeNull().EventCount.ShouldBe(7,
			"the delegate must really skip the already-folded event rather than repeat a write with its own refresh");
		A.CallTo(() => barrier.EnsureVisibleAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		(await checkpoints.GetCheckpointAsync("visibility-host", AsyncProjectionProcessingHost.ProjectionKeyFor(typeof(OrderSummary)), safety.Token).ConfigureAwait(false)).ShouldBe(1);
	}

	[Fact]
	public async Task HaltWithoutAcknowledgingWhenSearchVisibilityFails()
	{
		using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		var held = new InMemoryProjectionStore<OrderSummary>();
		var barrier = A.Fake<IProjectionVisibilityBarrier>();
		A.CallTo(() => barrier.EnsureVisibleAsync(A<CancellationToken>._)).ThrowsAsync(new InvalidOperationException("refresh refused"));
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		await using var provider = Build(new Stream(), held, barrier, checkpoints);
		var result = await Driver(provider).ProcessPendingAsync(safety.Token).ConfigureAwait(false);
		result.Outcome.ShouldBe(ProjectionProcessingOutcome.Halted);
		(await held.GetByIdAsync("order-1", safety.Token).ConfigureAwait(false)).ShouldNotBeNull().EventCount.ShouldBe(1);
		(await checkpoints.GetCheckpointAsync("visibility-host", AsyncProjectionProcessingHost.ProjectionKeyFor(typeof(OrderSummary)), safety.Token).ConfigureAwait(false)).ShouldBeNull();
		A.CallTo(() => barrier.EnsureVisibleAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task FailStartAsyncOnAnIncompatibleDefinitionBeforeStartingTheBackgroundTask()
	{
		using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		await using var first = Build(new Stream(), new InMemoryProjectionStore<OrderSummary>(),
			A.Fake<IProjectionVisibilityBarrier>(), checkpoints);
		await Driver(first).ProcessPendingAsync(safety.Token).ConfigureAwait(false);
		var otherQuery = new Stream();
		await using var second = Build(otherQuery, new InMemoryProjectionStore<OrderSummary>(),
			A.Fake<IProjectionVisibilityBarrier>(), checkpoints, revision: "2");
		var incompatible = Driver(second);
		try
		{
			await Should.ThrowAsync<InvalidOperationException>(() => incompatible.StartAsync(safety.Token));
			incompatible.ExecuteTask.ShouldBeNull(".NET 10 startup validation must precede base.StartAsync");
			otherQuery.Reads.ShouldBe(0);
			(await checkpoints.GetCheckpointAsync("visibility-host", AsyncProjectionProcessingHost.ProjectionKeyFor(typeof(OrderSummary)), safety.Token).ConfigureAwait(false)).ShouldBe(1);
		}
		finally
		{
			await incompatible.StopAsync(safety.Token).ConfigureAwait(false);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RefuseAnAsyncRegistrationWithoutAnApplyDelegateBeforeScanning(bool startWorker)
	{
		using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		var query = new Stream();
		var barrier = A.Fake<IProjectionVisibilityBarrier>();
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		await using var provider = Build(query, new InMemoryProjectionStore<OrderSummary>(), barrier, checkpoints);
		var host = Driver(provider);
		provider.GetRequiredService<IProjectionRegistry>().Register(new ProjectionRegistration(typeof(OrderSummary),
			ProjectionMode.Async, new MultiStreamProjection<OrderSummary>(), inlineApply: null));
		try
		{
			if (startWorker)
				await Should.ThrowAsync<InvalidOperationException>(() => host.StartAsync(safety.Token));
			else
				await Should.ThrowAsync<InvalidOperationException>(() => host.ProcessPendingAsync(safety.Token));
			query.Reads.ShouldBe(0);
			A.CallTo(() => barrier.EnsureVisibleAsync(A<CancellationToken>._)).MustNotHaveHappened();
			(await checkpoints.GetCheckpointAsync("visibility-host", AsyncProjectionProcessingHost.ProjectionKeyFor(typeof(OrderSummary)), safety.Token).ConfigureAwait(false)).ShouldBeNull();
		}
		finally
		{
			await host.StopAsync(safety.Token).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task StopAtTheCapturedHeadEvenWhenEveryScanCanDiscoverAnotherEvent()
	{
		using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		var query = new Stream(growing: true);
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		var held = new InMemoryProjectionStore<OrderSummary>();
		await using var provider = Build(query, held, A.Fake<IProjectionVisibilityBarrier>(), checkpoints);
		var result = await Driver(provider).ProcessPendingAsync(safety.Token).ConfigureAwait(false);
		result.Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);
		result.EventsApplied.ShouldBe(1);
		query.Reads.ShouldBe(1, "a perpetually growing source must not turn one request into a lifetime drain");
		(await checkpoints.GetCheckpointAsync("visibility-host", AsyncProjectionProcessingHost.ProjectionKeyFor(typeof(OrderSummary)), safety.Token).ConfigureAwait(false)).ShouldBe(1);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RefuseAChangedConfiguredDestinationBeforeScanningOrEffects(bool startWorker)
	{
		using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		await using var original = Build(new Stream(), new InMemoryProjectionStore<OrderSummary>(),
			A.Fake<IProjectionVisibilityBarrier>(), checkpoints, destination: "elasticsearch/people-employees");
		await Driver(original).ProcessPendingAsync(safety.Token).ConfigureAwait(false);
		var otherQuery = new Stream();
		var otherHeld = new InMemoryProjectionStore<OrderSummary>();
		var otherBarrier = A.Fake<IProjectionVisibilityBarrier>();
		// Only the configured destination changes. Source/scope, revision, projection type and
		// subscription name are identical, reproducing a bounded-context prefix or index override drift.
		await using var changed = Build(otherQuery, otherHeld, otherBarrier, checkpoints,
			destination: "elasticsearch/payroll-employees");
		var host = Driver(changed);
		try
		{
			if (startWorker)
				await Should.ThrowAsync<InvalidOperationException>(() => host.StartAsync(safety.Token));
			else
				await Should.ThrowAsync<InvalidOperationException>(() => host.ProcessPendingAsync(safety.Token));
			otherQuery.Reads.ShouldBe(0);
			(await otherHeld.GetByIdAsync("order-1", safety.Token).ConfigureAwait(false)).ShouldBeNull();
			A.CallTo(() => otherBarrier.EnsureVisibleAsync(A<CancellationToken>._)).MustNotHaveHappened();
			(await checkpoints.GetCheckpointAsync("visibility-host", AsyncProjectionProcessingHost.ProjectionKeyFor(typeof(OrderSummary)), safety.Token).ConfigureAwait(false)).ShouldBe(1);
			if (startWorker) host.ExecuteTask.ShouldBeNull();
		}
		finally
		{
			await host.StopAsync(safety.Token).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task AllowCooperationWhenTheConfiguredDestinationMatches()
	{
		using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		var held = new InMemoryProjectionStore<OrderSummary>();
		await using var original = Build(new Stream(), held,
			A.Fake<IProjectionVisibilityBarrier>(), checkpoints, destination: "elasticsearch/people-employees");
		await Driver(original).ProcessPendingAsync(safety.Token).ConfigureAwait(false);
		await using var matching = Build(new Stream(), held,
			A.Fake<IProjectionVisibilityBarrier>(), checkpoints, destination: "elasticsearch/people-employees");
		var result = await Driver(matching).ProcessPendingAsync(safety.Token).ConfigureAwait(false);
		result.Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);
		(await held.GetByIdAsync("order-1", safety.Token).ConfigureAwait(false)).ShouldNotBeNull().EventCount.ShouldBe(1);
		(await checkpoints.GetCheckpointAsync("visibility-host", AsyncProjectionProcessingHost.ProjectionKeyFor(typeof(OrderSummary)), safety.Token).ConfigureAwait(false)).ShouldBe(1);
	}

	[Fact]
	public async Task RefuseDestinationDriftBetweenBindingAndTheActualApplyScope()
	{
		using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		var held = new InMemoryProjectionStore<OrderSummary>();
		var barrier = A.Fake<IProjectionVisibilityBarrier>();
		var query = new Stream();
		var resolvedTargets = new List<string>();
		await using var provider = Build(query, held, barrier, checkpoints, destinationForScope: resolution =>
		{
			var identity = resolution == 1 ? "elasticsearch/people-employees" : "elasticsearch/payroll-employees";
			resolvedTargets.Add(identity);
			return identity;
		});
		// Binding probes scope A. The same registered scoped-store factory resolves a changed
		// configured target in apply scope B, reproducing an options reload between those scopes.
		var result = await Driver(provider).ProcessPendingAsync(safety.Token).ConfigureAwait(false);
		result.Outcome.ShouldBe(ProjectionProcessingOutcome.Halted);
		resolvedTargets.Take(2).ShouldBe(["elasticsearch/people-employees", "elasticsearch/payroll-employees"]);
		query.Reads.ShouldBe(1, "an actual event page must reach dispatch, rather than passing through an idle path");
		(await held.GetByIdAsync("order-1", safety.Token).ConfigureAwait(false)).ShouldBeNull(
			"the actual apply scope's destination must be checked before invoking its fold delegate");
		(await checkpoints.GetCheckpointAsync("visibility-host", AsyncProjectionProcessingHost.ProjectionKeyFor(typeof(OrderSummary)), safety.Token).ConfigureAwait(false)).ShouldBeNull();
		A.CallTo(() => barrier.EnsureVisibleAsync(A<CancellationToken>._)).MustNotHaveHappened();
	}

	// Resolve the HOST, not a cast of the pull interface. IProjectionProcessor is deliberately a
	// composite over every registered mode now, so casting it to one mode's host asserts the wiring
	// shape rather than anything these arms are about. The host is registered in its own right.
	private static AsyncProjectionProcessingHost Driver(IServiceProvider provider) =>
		provider.GetRequiredService<AsyncProjectionProcessingHost>();

	private static ServiceProvider Build(Stream query, InMemoryProjectionStore<OrderSummary> held,
		IProjectionVisibilityBarrier barrier, ISubscriptionCheckpointStore checkpoints, string revision = "1",
		string destination = "test-default", Func<int, string>? destinationForScope = null)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		if (destinationForScope is null)
		{
			services.AddSingleton<IProjectionStore<OrderSummary>>(new CapabilitySurface(held, barrier, destination));
		}
		else
		{
			var resolutions = 0;
			services.AddScoped<IProjectionStore<OrderSummary>>(_ =>
				new CapabilitySurface(held, barrier, destinationForScope(Interlocked.Increment(ref resolutions))));
		}
		services.AddExcalibur(configuration => configuration.AddEventSourcing(es =>
		{
			es.UseInMemory();
			es.AddProjection<OrderSummary>(projection => projection.Async().When<TestOrderPlaced>((state, _) => state.EventCount++));
			es.AddProjectionProcessor(options =>
			{
				options.ProjectionName = "visibility-host";
				options.SubscriptionScope = "unit-source-and-destination";
				options.SubscriptionRevision = revision;
				options.BatchSize = 1;
				options.IdlePollingInterval = TimeSpan.FromHours(1);
			});
		}));
		var serializer = A.Fake<IEventSerializer>();
		A.CallTo(() => serializer.ResolveType(A<string>._)).Returns(typeof(TestOrderPlaced));
		A.CallTo(() => serializer.DeserializeEvent(A<byte[]>._, A<Type>._)).Returns(new TestOrderPlaced());
		services.AddSingleton(serializer);
		services.AddSingleton<IGlobalStreamQuery>(query);
		services.AddSingleton(checkpoints);
		return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
	}

	// Handwritten instead of a proxy over the internal shared DTO: the capability seam is the
	// behavior under test, while projection state and its CAS are provided by the real store.
	private sealed class CapabilitySurface(InMemoryProjectionStore<OrderSummary> held,
		IProjectionVisibilityBarrier barrier, string destination) : IProjectionStore<OrderSummary>
	{
		public object? GetService(Type serviceType) =>
            serviceType == typeof(IProjectionTenantBoundary) ? ProjectionTenantBoundary.EstateWide :
			serviceType == typeof(IPositionedProjectionStore<OrderSummary>) ? held
			: serviceType == typeof(IProjectionVisibilityBarrier) ? barrier
			: serviceType == typeof(IProjectionDestinationIdentity) ? new DestinationIdentity(destination)
			: serviceType.IsInstanceOfType(this) ? this : null;
		public Task<OrderSummary?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
			held.GetByIdAsync(id, cancellationToken);
		public Task UpsertAsync(string id, OrderSummary projection, CancellationToken cancellationToken) =>
			held.UpsertAsync(id, projection, cancellationToken);
		public Task DeleteAsync(string id, CancellationToken cancellationToken) => held.DeleteAsync(id, cancellationToken);
		public Task<IReadOnlyList<OrderSummary>> QueryAsync(IDictionary<string, object>? filters,
			QueryOptions? options, CancellationToken cancellationToken) => held.QueryAsync(filters, options, cancellationToken);
		public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) =>
			held.CountAsync(filters, cancellationToken);
	}

	private sealed class DestinationIdentity(string identity) : IProjectionDestinationIdentity
	{
		public string Identity { get; } = identity;
	}

	private sealed class Stream(bool growing = false) : IGlobalStreamQuery
	{
		internal int Reads { get; private set; }
		public ValueTask<long> GetHeadPositionAsync(CancellationToken cancellationToken) =>
			ValueTask.FromResult(growing ? Reads + 1L : 1L);
		public ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(GlobalStreamPosition position, int maxCount,
			CancellationToken cancellationToken)
		{
			Reads++;
			var next = position.Position + 1;
			return ValueTask.FromResult<IReadOnlyList<StoredEvent>>(growing || position.Position == 0
				? [new StoredEvent($"event-{next}", "order-1", "Order", "TestOrderPlaced", [1], null, next,
					DateTimeOffset.UnixEpoch) { GlobalPosition = next }]
				: []);
		}
		public ValueTask<IReadOnlyList<StoredEvent>> ReadByEventTypeAsync(string eventType, GlobalStreamPosition position,
			int maxCount, CancellationToken cancellationToken) => ReadAllAsync(position, maxCount, cancellationToken);
	}
}
