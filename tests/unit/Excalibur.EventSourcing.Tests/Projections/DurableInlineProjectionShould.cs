// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;
using Excalibur.Dispatch;
using Microsoft.Extensions.Hosting;
using Counter = Excalibur.EventSourcing.Tests.Projections.DurableInlineCounter;

namespace Excalibur.EventSourcing.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Integration")]
public sealed class DurableInlineProjectionShould
{
	[Fact]
	public void RegisterForegroundProcessingAndRecoveryWithoutASecondEnableCall()
	{
		var services = new ServiceCollection();
		var builder = A.Fake<IEventSourcingBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		builder.AddProjection<Counter>(p => p.Inline().When<TestOrderPlaced>((p, _) => p.Count++));
		services.ShouldContain(d => d.ServiceType == typeof(IHostedService)
			&& d.ImplementationType == typeof(InlineProjectionHostedService));
		services.ShouldNotContain(d => d.ServiceType == typeof(IHostedService)
			&& d.ImplementationType == typeof(AsyncProjectionProcessingHostedService));
	}

	[Fact]
	public void BindInlineToThePositionedFoldWithoutRenumberingAsyncOrEphemeral()
	{
		var registry = new InMemoryProjectionRegistry();
		var builder = new ProjectionBuilder<Counter>(registry);
		builder.Inline().When<TestOrderPlaced>((p, _) => p.Count++);
		builder.Build();
		var registration = registry.GetRegistration(typeof(Counter)).ShouldNotBeNull();
		registration.Mode.ShouldBe(ProjectionMode.Inline);
		registration.InlineApply.ShouldNotBeNull();
		((int)ProjectionMode.Async).ShouldBe(0);
		((int)ProjectionMode.Ephemeral).ShouldBe(1);
	}


	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	public async Task FoldTheWholeCommittedPrefixOnceAcrossIndependentApplications(int applications)
	{
		var fixture = new Pipeline(20);
		await using var api = fixture.CreateProvider();
		await using var jobs = fixture.CreateProvider();
		var first = Notify(api, 20);
		if (applications == 2) { await Task.WhenAll(first, Notify(jobs, 20)); }
		else { await first; }
		(await fixture.Store.GetByIdAsync("row", CancellationToken.None)).ShouldNotBeNull().Count.ShouldBe(20);
		fixture.Writes.ShouldBe(1, "one batch folds twenty events into one document; replay must not rewrite it");
		await Notify(jobs, 20);
		fixture.Writes.ShouldBe(1, "another process observing the completed checkpoint must not issue a write");
	}

	[Fact]
	public async Task ResumeAfterAnAcknowledgementIsLostWithoutRewritingTheDocument()
	{
		var fixture = new Pipeline(3) { LoseWriteAcknowledgement = true };
		await using (var first = fixture.CreateProvider())
		{
			(await Notify(first, 3)).ShouldBe(ProjectionFreshness.BoundExpired,
				"a write whose acknowledgement was lost cannot be confirmed, so freshness must not be claimed");
			fixture.Writes.ShouldBe(1);
			// THE PER-REGISTRATION KEY, not the old single-cursor name. Asserting the old name would pass
			// VACUOUSLY now that cursors are keyed per projection: no such cursor exists, so the store
			// answers null for any question about it, and the arm would stop binding anything at all.
			(await fixture.Checkpoints.GetCheckpointAsync(
				$"Inline:AsyncProjectionProcessingHost:{typeof(Counter).FullName}", CancellationToken.None))
				.ShouldBeNull("the cursor must not advance over a write whose acknowledgement was lost");
		}
		fixture.LoseWriteAcknowledgement = false;
		await using var restarted = fixture.CreateProvider();
		await Notify(restarted, 3);
		fixture.Writes.ShouldBe(1, "an ambiguous successful document write is reconciled by its recorded position");
		(await fixture.Store.GetByIdAsync("row", CancellationToken.None)).ShouldNotBeNull().Count.ShouldBe(3);
	}

	[Fact]
	public async Task KeepAsyncHandlersOffTheInlineSavePath()
	{
		var fixture = new Pipeline(1);
		await using var provider = fixture.CreateProvider(includeBrokenAsync: true);
		await Notify(provider, 1);
		(await fixture.Store.GetByIdAsync("row", CancellationToken.None)).ShouldNotBeNull().Count.ShouldBe(1);
	}

	/// <summary>
	/// A bulk writer can decline the inline wait for a block of saves, and is TOLD it declined.
	/// </summary>
	/// <remarks>
	/// The wait exists so a request can search for its own write. A backfill has no such reader, and
	/// pays the whole cost per record because the consumer is sequential. Suppressing it must not look
	/// like success: the result says Deferred, which is neither Fresh nor NotRequested.
	/// </remarks>
	[Fact]
	public async Task DeferTheInlineFoldInsideASuppressionScope()
	{
		var fixture = new Pipeline(1);
		await using var provider = fixture.CreateProvider();

		ProjectionFreshness freshness;
		using (InlineProjectionWait.Suppress())
		{
			freshness = await Notify(provider, 1);
		}

		freshness.ShouldBe(ProjectionFreshness.Deferred,
			"a suppressed wait must report that it did not wait, never that the read model is current");
		fixture.Writes.ShouldBe(0, "the save must not fold inline while the wait is suppressed");
	}

	/// <summary>LIVENESS. Outside the scope the guarantee is unchanged.</summary>
	[Fact]
	public async Task StillWaitForTheInlineFoldOutsideASuppressionScope()
	{
		var fixture = new Pipeline(1);
		await using var provider = fixture.CreateProvider();

		(await Notify(provider, 1)).ShouldBe(ProjectionFreshness.Fresh);
		(await fixture.Store.GetByIdAsync("row", CancellationToken.None)).ShouldNotBeNull().Count.ShouldBe(1);
	}

	/// <summary>
	/// The scope restores what it found, so a suppression cannot leak past its block and silently
	/// disarm the guarantee for everything that follows.
	/// </summary>
	[Fact]
	public async Task RestoreTheAmbientWaitWhenTheScopeEnds()
	{
		var fixture = new Pipeline(1);
		await using var provider = fixture.CreateProvider();

		using (InlineProjectionWait.Suppress())
		{
			using (InlineProjectionWait.Suppress())
			{
				(await Notify(provider, 1)).ShouldBe(ProjectionFreshness.Deferred);
			}

			(await Notify(provider, 1)).ShouldBe(ProjectionFreshness.Deferred,
				"the inner scope must restore the OUTER suppression, not clear suppression entirely");
		}

		(await Notify(provider, 1)).ShouldBe(ProjectionFreshness.Fresh,
			"once every scope has ended the wait is in force again");
	}

	[Fact]
	public async Task RecoverCommittedWorkOnHostStartupWithoutAnotherSave()
	{
		var fixture = new Pipeline(1);
		await using var provider = fixture.CreateProvider();
		var coordinator = provider.GetRequiredService<InlineProjectionCoordinator>();
		await coordinator.StartAsync(CancellationToken.None);
		try { await fixture.Written.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
		finally { await coordinator.StopAsync(CancellationToken.None); }
		(await fixture.Store.GetByIdAsync("row", CancellationToken.None)).ShouldNotBeNull().Count.ShouldBe(1);
	}

	// vvxkgq LIVENESS: a host that runs NO hosted service must still be able to drive Inline.
	//
	// The pull entry point used to resolve to the ASYNC host alone, so in a host-less container nothing
	// drove Inline at all: the background loop that recovers an incomplete fold is an IHostedService,
	// which such a host never starts. A timed-out fold was then recovered only by the NEXT save, and the
	// last save before the process went idle was never recovered. Note no coordinator.StartAsync here --
	// that is the whole point.
	[Fact]
	public async Task RecoverThroughThePullProcessorWithNoHostedServiceRunning()
	{
		var fixture = new Pipeline(3);
		await using var provider = fixture.CreateProvider();

		var result = await provider.GetRequiredService<IProjectionProcessor>()
			.ProcessPendingAsync(CancellationToken.None);

		result.Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp,
			"the pull entry point must drive the Inline cursor, not only the async one");
		(await fixture.Store.GetByIdAsync("row", CancellationToken.None)).ShouldNotBeNull().Count.ShouldBe(3);
		fixture.Writes.ShouldBe(1);
	}

	[Fact]
	public async Task CompleteAnImmutableInlineProjectionWithoutAnExplicitDriver()
	{
		var fixture = new Pipeline(1);
		await using var provider = fixture.CreateProvider(immutable: true);
		await Notify(provider, 1);
		(await fixture.Store.GetByIdAsync("row", CancellationToken.None)).ShouldNotBeNull().Count.ShouldBe(1);
		fixture.Writes.ShouldBe(1);
	}

	[Fact]
	public async Task RejectInvalidInlineOptionsBeforeStartingRecovery()
	{
		foreach (var change in new Action<GlobalStreamProjectionOptions>[]
		{
			o => o.BatchSize = 0,
			o => o.IdlePollingInterval = TimeSpan.Zero,
			o => o.ProjectionName = " ",
		})
		{
			var fixture = new Pipeline(1);
			await using var provider = fixture.CreateProvider(configure: services => services.Configure(change));
			await Should.ThrowAsync<Microsoft.Extensions.Options.OptionsValidationException>(() =>
				provider.GetRequiredService<InlineProjectionCoordinator>().StartAsync(CancellationToken.None));
			fixture.Writes.ShouldBe(0);
		}
	}

	[Fact]
	public async Task DeliverNotificationsOnceInRegistrationOrderAcrossTwoProjectionDefinitions()
	{
		var calls = new List<int>();
		var first = A.Fake<IEventNotificationHandler<TestOrderPlaced>>();
		var second = A.Fake<IEventNotificationHandler<TestOrderPlaced>>();
		A.CallTo(() => first.HandleAsync(A<TestOrderPlaced>._, A<EventNotificationContext>._, A<CancellationToken>._))
			.Invokes(() => calls.Add(1)).Returns(Task.CompletedTask);
		A.CallTo(() => second.HandleAsync(A<TestOrderPlaced>._, A<EventNotificationContext>._, A<CancellationToken>._))
			.Invokes(() => calls.Add(2)).Returns(Task.CompletedTask);
		var fixture = new Pipeline(1);
		await using var provider = fixture.CreateProvider(includeBrokenAsync: true, configure: services =>
		{
			services.AddScoped(_ => first);
			services.AddScoped(_ => second);
		});
		await Notify(provider, 1);
		calls.ShouldBe([1, 2]);
	}

	[Fact]
	public async Task PreserveAHandlerThrownReflectionExceptionOnTypedDispatch()
	{
		var failure = new System.Reflection.TargetInvocationException(new InvalidOperationException("handler-owned"));
		var handler = A.Fake<IEventNotificationHandler<TestOrderPlaced>>();
		A.CallTo(() => handler.HandleAsync(A<TestOrderPlaced>._, A<EventNotificationContext>._, A<CancellationToken>._)).Throws(failure);
		var fixture = new Pipeline(1);
		await using var provider = fixture.CreateProvider(configure: services =>
		{
			services.AddSingleton(handler);
			services.Configure<EventNotificationOptions>(o => o.FailurePolicy = NotificationFailurePolicy.Propagate);
		});
		var actual = await Should.ThrowAsync<System.Reflection.TargetInvocationException>(() => Notify(provider, 1));
		actual.ShouldBeSameAs(failure);
	}

	[Fact]
	public async Task RefuseAStaleWriterWhenAnotherProcessFinishesItsPrefix()
	{
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var fixture = new Pipeline(4);
		var attempts = 0;
		fixture.BeforeWrite = async () =>
		{
			if (Interlocked.Increment(ref attempts) == 1)
			{
				entered.SetResult();
				await release.Task;
			}
		};
		await using var api = fixture.CreateProvider();
		await using var jobs = fixture.CreateProvider();
		var delayed = Notify(api, 4);
		await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
		try { await Notify(jobs, 4); }
		finally { release.TrySetResult(); }
		await delayed;
		(await fixture.Store.GetByIdAsync("row", CancellationToken.None)).ShouldNotBeNull().Count.ShouldBe(4);
		fixture.Writes.ShouldBe(2, "two competing attempts, then the loser reconciles instead of rewriting");
		await Notify(api, 4);
		fixture.Writes.ShouldBe(2);
	}

	[Fact]
	public async Task BoundTheForegroundWaitWithoutReleasingAnUnfinishedWrite()
	{
		var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var fixture = new Pipeline(1) { BeforeWrite = async () => { entered.TrySetResult(); await release.Task; } };
		await using var provider = fixture.CreateProvider(clock: clock);
		var save = Notify(provider, 1);
		await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
		clock.Advance(TimeSpan.FromSeconds(30));
		(await save.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBe(ProjectionFreshness.BoundExpired,
			"the budget expired, so the read model is behind -- the events are still committed");
		var second = Notify(provider, 1);
		fixture.Writes.ShouldBe(1, "a timed-out writer still owns the local page lease");
		release.SetResult();
		await second.WaitAsync(TimeSpan.FromSeconds(5));
		(await fixture.Store.GetByIdAsync("row", CancellationToken.None)).ShouldNotBeNull().Count.ShouldBe(1);
	}

	[Fact]
	public async Task DiscoverScopeWithAsynchronouslyDisposedStores()
	{
		var fixture = new Pipeline(1);
		await using var provider = fixture.CreateProvider(automaticScope: true);
		await Notify(provider, 1);
		fixture.Writes.ShouldBe(1);
	}

	[Fact]
	public async Task CompleteOnlyTheRequestedPrefixBeforeALaterPoisonEvent()
	{
		var fixture = new Pipeline(3) { PoisonPosition = 3 };
		await using var provider = fixture.CreateProvider();
		await Notify(provider, 2);
		(await fixture.Store.GetByIdAsync("row", CancellationToken.None)).ShouldNotBeNull().Count.ShouldBe(2);
		(await Notify(provider, 3)).ShouldBe(ProjectionFreshness.BoundExpired,
			"an undeserializable event at the requested position cannot be folded");
		fixture.Writes.ShouldBe(1);
	}

	[Fact]
	public async Task RejectAReaderReplacedIndependentlyOfItsSourceBinding()
	{
		var fixture = new Pipeline(1);
		await using var provider = fixture.CreateProvider(replaceReader: true);
		// Still an EXCEPTION, deliberately: a reader replaced independently of its checkpoint binding is a
		// misconfiguration, not a read model that is merely behind. Freshness describes whether the wait
		// succeeded; it must not be used to report a composition that can never work.
		var error = await Should.ThrowAsync<InvalidOperationException>(() => Notify(provider, 1));
		error.Message.ShouldContain("replaced independently");
		fixture.Writes.ShouldBe(0);
	}

	// LIVENESS half of the retry contract. Its SAFETY half is
	// ResumeAfterAnAcknowledgementIsLostWithoutRewritingTheDocument, which asserts that a TERMINAL halt
	// still reports pending. Both arms are required: retrying everything converts an unconfirmed write
	// into a reported success, and retrying nothing fails a committed save for a stop that clears on its
	// own. Only the reason tells those apart, which is why the halt reason exists.
	[Fact]
	public async Task RetryATransientlyUnreadablePrefixInsteadOfFailingTheCommittedSave()
	{
		var fixture = new Pipeline(1) { EmptyReadsBeforeProgress = 1 };
		await using var provider = fixture.CreateProvider();

		// The first read sees no readable prefix -> Halted/PrefixUnavailable. The save must wait inside
		// its budget rather than telling the caller a durable write is pending.
		(await Notify(provider, 1)).ShouldBe(ProjectionFreshness.Fresh,
			"a transient prefix must be waited out and then reported FRESH, not merely not-thrown");

		(await fixture.Store.GetByIdAsync("row", CancellationToken.None)).ShouldNotBeNull().Count.ShouldBe(1);
		fixture.Writes.ShouldBe(1, "the retry must fold the prefix once, not rewrite the document per attempt");
	}

	[Fact]
	public async Task ReturnCommittedPendingWhenTheStoreDoesNotSupplyAToken()
	{
		var fixture = new Pipeline(1);
		await using var provider = fixture.CreateProvider();
		(await Notify(provider, null)).ShouldBe(ProjectionFreshness.BoundExpired,
			"with no global position there is nothing to wait on, so freshness cannot be established");
		fixture.Writes.ShouldBe(0);
	}

	private static Task<ProjectionFreshness> Notify(ServiceProvider provider, long? token) =>
		provider.GetRequiredService<IEventNotificationBroker>().NotifyAsync([new TestOrderPlaced()],
			new EventNotificationContext("row", "Order", token ?? 1, DateTimeOffset.UtcNow, GlobalPosition: token));

    [Fact]
    public async Task ReturnTheCommittedTokenWhenTheGlobalReadFailsAfterAppend()
    {
        var fixture = new Pipeline(1);
        await using var provider = fixture.CreateProvider(configure: services =>
        {
            var builder = A.Fake<IEventSourcingBuilder>();
            A.CallTo(() => builder.Services).Returns(services);
            builder.AddProjection<Counter>(p => p.Inline()
                .When<EventSourcedRepositoryShould.TestCreatedEventV1>((state, _) => state.Count++));
        });
        var eventStore = A.Fake<IEventStore>();
        A.CallTo(() => eventStore.AppendAsync(A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._,
                A<long>._, A<CancellationToken>._))
            .Invokes(() => fixture.ReadFailure = new IOException("checkpoint/global reader connection lost after commit"))
            .Returns(AppendResult.CreateSuccess(0, 1, 1));
        var repository = new Excalibur.EventSourcing.Implementation.EventSourcedRepository<EventSourcedRepositoryShould.TestAggregate>(
            eventStore, provider.GetRequiredService<IEventSerializer>(), id => new EventSourcedRepositoryShould.TestAggregate(id),
            Microsoft.Extensions.Options.Options.Create(new Excalibur.EventSourcing.Implementation.EventSourcedRepositoryOptions()),
            eventNotificationBroker: provider.GetRequiredService<IEventNotificationBroker>());
        var aggregate = new EventSourcedRepositoryShould.TestAggregate("row");
        aggregate.Create("committed");
        var result = await repository.SaveAsync(aggregate, CancellationToken.None);
        result.SessionToken.ShouldBe(1);
        result.Projections.ShouldBe(ProjectionFreshness.BoundExpired);
        A.CallTo(() => eventStore.AppendAsync(A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._,
            A<long>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

	private sealed class Pipeline
	{
		private readonly IGlobalStreamQuery _query = A.Fake<IGlobalStreamQuery>();
		private readonly IEventSerializer _serializer = A.Fake<IEventSerializer>();
		private readonly IProjectionStore<Counter> _facade = A.Fake<IProjectionStore<Counter>>(o => o.Implements<IAsyncDisposable>());
		private readonly IPositionedProjectionStore<Counter> _positioned = A.Fake<IPositionedProjectionStore<Counter>>();
		internal InMemoryProjectionStore<Counter> Store { get; } = new();
		internal InMemorySubscriptionCheckpointStore Checkpoints { get; } = new();
		internal TaskCompletionSource Written { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		internal int Writes { get; private set; }
		internal bool LoseWriteAcknowledgement { get; set; }
		internal int? PoisonPosition { get; set; }
		internal Func<Task>? BeforeWrite { get; set; }

		/// <summary>
		/// Returns an EMPTY page this many times before the stream reads normally, which is what a
		/// not-yet-readable contiguous prefix looks like to the host: the head position is known, but the
		/// events below it are not yet visible because an append is still in flight. This is the one halt
		/// cause that clears by waiting, so it is the one the inline save path may retry.
		/// </summary>
		internal int EmptyReadsBeforeProgress { get; set; }
        internal Exception? ReadFailure { get; set; }

		internal Pipeline(int count)
		{
			var events = Enumerable.Range(1, count).Select(i => new StoredEvent(i.ToString(), "row", "Order",
				"placed", BitConverter.GetBytes(i), null, i, DateTimeOffset.UtcNow) { GlobalPosition = i }).ToArray();
			A.CallTo(() => _query.GetHeadPositionAsync(A<CancellationToken>._)).Returns(count);
			A.CallTo(() => _query.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
				.ReturnsLazily((GlobalStreamPosition p, int n, CancellationToken _) =>
				{
					if (ReadFailure is { } failure) { throw failure; }
					if (EmptyReadsBeforeProgress > 0)
					{
						EmptyReadsBeforeProgress--;
						return ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);
					}

					return ValueTask.FromResult<IReadOnlyList<StoredEvent>>(
						events.Where(e => e.GlobalPosition > p.Position).Take(n).ToArray());
				});
			A.CallTo(() => _serializer.ResolveType(A<string>._)).Returns(typeof(TestOrderPlaced));
			A.CallTo(() => _serializer.DeserializeEvent(A<byte[]>._, A<Type>._)).ReturnsLazily((byte[] payload, Type _) =>
				BitConverter.ToInt32(payload) == PoisonPosition ? throw new InvalidOperationException("poison") : new TestOrderPlaced());
			A.CallTo(() => _facade.GetService(typeof(IPositionedProjectionStore<Counter>))).Returns(_positioned);
			var destination = A.Fake<IProjectionDestinationScope>();
			A.CallTo(() => destination.Scope).Returns("test-cluster");
			A.CallTo(() => _facade.GetService(typeof(IProjectionDestinationScope))).Returns(destination);
			A.CallTo(() => _positioned.GetWithPositionAsync(A<string>._, A<CancellationToken>._))
				.ReturnsLazily((string id, CancellationToken ct) => Store.GetWithPositionAsync(id, ct));
			A.CallTo(() => _positioned.UpsertAtPositionAsync(A<string>._, A<Counter>._, A<long?>._, A<long>._, A<CancellationToken>._))
				.ReturnsLazily(async (string id, Counter state, long? expected, long position, CancellationToken ct) =>
				{
					Writes++;
					if (BeforeWrite is { } before) { await before(); }
					var result = await Store.UpsertAtPositionAsync(id, state, expected, position, ct);
					Written.TrySetResult();
					if (LoseWriteAcknowledgement) { throw new TaskCanceledException("Elasticsearch PUT response lost after commit"); }
					return result;
				});
		}

		internal ServiceProvider CreateProvider(bool includeBrokenAsync = false, bool replaceReader = false, bool automaticScope = false,
			TimeProvider? clock = null, Action<IServiceCollection>? configure = null, bool immutable = false)
		{
			var services = new ServiceCollection();
			services.AddLogging();
			if (clock is not null) { services.AddSingleton(clock); }
			services.AddSingleton(_query);
			services.AddSingleton(_serializer);
			services.AddSingleton(new InlineProjectionSource("test-source", Checkpoints, _query));
			if (replaceReader) { services.AddSingleton(A.Fake<IGlobalStreamQuery>()); }
			services.AddScoped(_ => _facade);
			if (!automaticScope) { services.Configure<GlobalStreamProjectionOptions>(o => o.SubscriptionScope = "test"); }
			var builder = A.Fake<IEventSourcingBuilder>();
			A.CallTo(() => builder.Services).Returns(services);
			if (immutable)
			{
				builder.AddImmutableProjection<Counter>(p => p.Inline().WhenCreating<TestOrderPlaced>(_ => new Counter { Count = 1 }));
			}
			else
			{
				builder.AddProjection<Counter>(p => p.Inline().When<TestOrderPlaced>((state, _) => state.Count++));
			}
			if (includeBrokenAsync)
			{
				builder.AddProjection<OrderSummary>(p => p.Async().When<TestOrderPlaced>((_, _) => throw new InvalidOperationException("Async is broken")));
			}
			configure?.Invoke(services);
			return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
		}
	}
}

/// <summary>Public state for the instrumented projection-store proxy.</summary>
public sealed class DurableInlineCounter { public int Count { get; set; } }
