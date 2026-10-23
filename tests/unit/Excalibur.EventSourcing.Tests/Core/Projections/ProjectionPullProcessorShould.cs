// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Tests.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// The pull API exists so a host that cannot run a BackgroundService -- a Function, a Lambda -- still
/// advances its projections. The property that matters is that it RETURNS: the same loop that idles and
/// polls forever for the hosted service must terminate for a pull caller.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ProjectionPullProcessorShould
{
	private readonly InMemoryProjectionRegistry _registry = new();
	private readonly InMemorySubscriptionCheckpointStore _checkpoints = new();

	// Real time, not a fake clock. A pull invocation that failed to terminate would otherwise hang the
	// suite instead of failing it, which is the trap found while proving the session reader's arms.
	private static CancellationTokenSource Safety() => new(TimeSpan.FromSeconds(30));

	private sealed class EmptyStream : IGlobalStreamQuery
	{
		public ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
			GlobalStreamPosition position, int maxCount, CancellationToken cancellationToken) =>
			ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);

		public ValueTask<IReadOnlyList<StoredEvent>> ReadByEventTypeAsync(
			string eventType, GlobalStreamPosition position, int maxCount, CancellationToken cancellationToken) =>
			ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);

		public ValueTask<long> GetHeadPositionAsync(CancellationToken cancellationToken) =>
			ValueTask.FromResult(0L);
	}

	// Signals the first read, so a test can tell that the worker is inside its polling loop -- and
	// therefore holding the cursor -- rather than guessing with a delay.
	private sealed class SignallingEmptyStream(TaskCompletionSource entered) : IGlobalStreamQuery
	{
		public ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
			GlobalStreamPosition position, int maxCount, CancellationToken cancellationToken)
		{
			_ = entered.TrySetResult();
			return ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);
		}

		public ValueTask<IReadOnlyList<StoredEvent>> ReadByEventTypeAsync(
			string eventType, GlobalStreamPosition position, int maxCount, CancellationToken cancellationToken) =>
			ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);

		public ValueTask<long> GetHeadPositionAsync(CancellationToken cancellationToken) =>
			ValueTask.FromResult(0L);
	}

	private static ProjectionRegistration AsyncRegistration() =>
		new(
			typeof(OrderSummary),
			ProjectionMode.Async,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (_, _, _, _) => Task.CompletedTask);

	private AsyncProjectionProcessingHost CreateHost(bool withStream = true)
	{
		var services = new ServiceCollection();
		if (withStream)
		{
			_ = services.AddSingleton<IGlobalStreamQuery>(new EmptyStream());
		}

		return new AsyncProjectionProcessingHost(
			_registry,
			A.Fake<IEventSerializer>(),
			_checkpoints,
			Options.Create(new GlobalStreamProjectionOptions { SubscriptionScope = "unit-test" }),
			services.BuildServiceProvider(),
			NullLogger<AsyncProjectionProcessingHost>.Instance);
	}

	[Fact]
	public async Task ReturnRatherThanIdle_WhenThereIsNothingLeftToApply()
	{
		// THE defining property. The hosted service reaching this state delays and polls again, forever.
		// A pull caller must get control back -- a serverless invocation that never returns is a billed
		// timeout and a projection that still did not advance.
		using var safety = Safety();
		using var host = CreateHost();
		_registry.Register(AsyncRegistration());

		var result = await host.ProcessPendingAsync(safety.Token);

		result.Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);
		result.EventsApplied.ShouldBe(0);
		safety.IsCancellationRequested.ShouldBeFalse("the pull invocation must return well inside its safety bound");
	}

	[Fact]
	public async Task ReturnImmediately_WhenNoAsyncProjectionsAreRegistered()
	{
		using var safety = Safety();
		using var host = CreateHost();

		var result = await host.ProcessPendingAsync(safety.Token);

		result.EventsApplied.ShouldBe(0);
		safety.IsCancellationRequested.ShouldBeFalse();
	}

	[Fact]
	public async Task BeReentrantAcrossInvocations()
	{
		// A serverless host calls this on every trigger, so the gate must release and the state must
		// reset between invocations. A gate that leaked would deadlock the second call, and a run counter
		// that did not reset would report the first invocation's work again.
		using var safety = Safety();
		using var host = CreateHost();
		_registry.Register(AsyncRegistration());

		var first = await host.ProcessPendingAsync(safety.Token);
		var second = await host.ProcessPendingAsync(safety.Token);

		first.Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);
		second.Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);
		second.EventsApplied.ShouldBe(0, "the per-invocation count must not carry over from the previous call");
	}

	private sealed class OneEventStream(long head) : IGlobalStreamQuery
	{
		public ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
			GlobalStreamPosition position, int maxCount, CancellationToken cancellationToken) =>
			ValueTask.FromResult<IReadOnlyList<StoredEvent>>(
				position.Position >= 1
					? []
					: [new StoredEvent("e1", "order-1", "Order", "OrderCreated", [], null, 1, DateTimeOffset.UtcNow) { GlobalPosition = 1 }]);

		public ValueTask<IReadOnlyList<StoredEvent>> ReadByEventTypeAsync(
			string eventType, GlobalStreamPosition position, int maxCount, CancellationToken cancellationToken) =>
			ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);

		public ValueTask<long> GetHeadPositionAsync(CancellationToken cancellationToken) =>
			ValueTask.FromResult(head);
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task NotReportAFirstDeliveryAsAReApplication()
	{
		// THE REQUIREMENT, as the consumer sees it: IsReplay means "this event is being RE-applied, so a
		// side effect already performed for it must not be performed again". An event this subscription
		// has never applied has had no side effect performed, so classifying it as a re-application makes
		// a handler honouring our documented obligation skip an effect that never happened.
		//
		// The pull path is where the classifier degenerates. The head is read at the start of every
		// invocation, and a pending event is at or below the current head by definition -- so on a
		// serverless host EVERY event, forever, is classified as history.
		using var safety = Safety();
		var observed = new List<bool>();
		var services = new ServiceCollection();
		_ = services.AddSingleton<IGlobalStreamQuery>(new OneEventStream(head: 1));
		var serializer = A.Fake<IEventSerializer>();
		A.CallTo(() => serializer.ResolveType(A<string>._)).Returns(typeof(TestOrderPlaced));
		A.CallTo(() => serializer.DeserializeEvent(A<byte[]>._, A<Type>._)).Returns(new TestOrderPlaced());
		using var host = new AsyncProjectionProcessingHost(
			_registry,
			serializer,
			_checkpoints,
			Options.Create(new GlobalStreamProjectionOptions { SubscriptionScope = "unit-test", CheckpointInterval = 1 }),
			services.BuildServiceProvider(),
			NullLogger<AsyncProjectionProcessingHost>.Instance);
		_registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Async,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (events, context, _, _) =>
			{
				observed.AddRange(events.Select(_ => context.IsReplay));
				return Task.CompletedTask;
			}));

		_ = await host.ProcessPendingAsync(safety.Token);

		observed.ShouldBe([false], "an event the subscription has never applied is a FIRST delivery");
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task ReturnPromptlyWhenAnIdleBackgroundWorkerHoldsTheCursor()
	{
		// THE AUDIT'S CASE. The hosted service held the serialisation gate for its whole lifetime,
		// including the interval it spends idle between polls, and the pull API waited on that same
		// gate without a bound. So a perfectly healthy idle worker starved every manual drive until
		// the process stopped -- and the registration that advertised the two as safe together is what
		// made it reachable. The same-instance arm proves there are never two cursors; it says nothing
		// about either driver making progress.
		using var safety = Safety();

		// Deterministic rather than timing-based: the worker takes the gate when ExecuteAsync begins,
		// and StartAsync does NOT await that, so calling the pull immediately is a race the pull can
		// win. Waiting until the worker has actually read the stream proves it is inside its loop and
		// therefore holding the cursor.
		var workerEnteredLoop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var services = new ServiceCollection();
		_ = services.AddSingleton<IGlobalStreamQuery>(new SignallingEmptyStream(workerEnteredLoop));

		using var host = new AsyncProjectionProcessingHost(
			_registry,
			A.Fake<IEventSerializer>(),
			_checkpoints,
			Options.Create(new GlobalStreamProjectionOptions
			{ SubscriptionScope = "unit-test",
				IdlePollingInterval = TimeSpan.FromMilliseconds(200),
			}),
			services.BuildServiceProvider(),
			NullLogger<AsyncProjectionProcessingHost>.Instance);

		_registry.Register(AsyncRegistration());

		await host.StartAsync(safety.Token).ConfigureAwait(false);
		try
		{
			await workerEnteredLoop.Task
				.WaitAsync(TimeSpan.FromSeconds(10), safety.Token).ConfigureAwait(false);

			var result = await host.ProcessPendingAsync(safety.Token).ConfigureAwait(false);

			result.Outcome.ShouldBe(
				ProjectionProcessingOutcome.CaughtUp,
				customMessage: "an idle worker releases the page gate, so a manual drive can finish cooperatively");
			result.EventsApplied.ShouldBe(0);
			safety.IsCancellationRequested.ShouldBeFalse(
				"the call must return well inside its bound rather than waiting out the worker");
		}
		finally
		{
			await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task StillDriveTheProcessorWhenNoWorkerIsRunning()
	{
		// LIVENESS, and the arm that stops the one above being satisfied by a pull API that always
		// refuses. With no worker started, the pull owns the cursor and must do the work.
		using var safety = Safety();
		using var host = CreateHost();
		_registry.Register(AsyncRegistration());

		var result = await host.ProcessPendingAsync(safety.Token).ConfigureAwait(false);

		result.Outcome.ShouldBe(ProjectionProcessingOutcome.CaughtUp);
	}
}
