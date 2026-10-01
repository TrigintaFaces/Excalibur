// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.Metrics;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.Projections;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InlineProjectionProcessorShould
{
	private readonly InMemoryProjectionRegistry _registry = new();
	private readonly IServiceScopeFactory _scopeFactory = A.Fake<IServiceScopeFactory>();
	private readonly InlineProjectionProcessor _processor;

	public InlineProjectionProcessorShould()
	{
		_processor = new InlineProjectionProcessor(
			_registry,
			_scopeFactory,
			NullLogger<InlineProjectionProcessor>.Instance);
	}

	private static EventNotificationContext CreateContext(string aggregateId = "agg-1") =>
		new(aggregateId, "TestAggregate", 1, DateTimeOffset.UtcNow);

	[Fact]
	public async Task NoOpWhenNoInlineRegistrations()
	{
		// Arrange -- no registrations in registry
		var events = new List<IDomainEvent> { new TestOrderPlaced() };

		// Act -- should complete without error
		await _processor.ProcessAsync(
			events,
			CreateContext(),
			NotificationFailurePolicy.Propagate,
			CancellationToken.None);
	}

	[Fact]
	public async Task InvokeInlineApplyDelegateForRegistration()
	{
		// Arrange
		var delegateInvoked = false;
		var registration = new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Inline,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (events, context, sp, ct) =>
			{
				delegateInvoked = true;
				return Task.CompletedTask;
			});
		_registry.Register(registration);

		var domainEvents = new List<IDomainEvent> { new TestOrderPlaced() };

		// Act
		await _processor.ProcessAsync(
			domainEvents,
			CreateContext(),
			NotificationFailurePolicy.Propagate,
			CancellationToken.None);

		// Assert
		delegateInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task SkipNonInlineRegistrations()
	{
		// Arrange -- register as Async, not Inline
		var delegateInvoked = false;
		var registration = new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Async,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (_, _, _, _) =>
			{
				delegateInvoked = true;
				return Task.CompletedTask;
			});
		_registry.Register(registration);

		// Act
		await _processor.ProcessAsync(
			new List<IDomainEvent> { new TestOrderPlaced() },
			CreateContext(),
			NotificationFailurePolicy.Propagate,
			CancellationToken.None);

		// Assert
		delegateInvoked.ShouldBeFalse();
	}

	[Fact]
	public async Task ThrowAggregateExceptionOnPropagatePolicy()
	{
		// Arrange
		var registration = new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Inline,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (_, _, _, _) =>
				Task.FromException(new InvalidOperationException("store failure")));
		_registry.Register(registration);

		// Act & Assert
		var ex = await Should.ThrowAsync<AggregateException>(() =>
			_processor.ProcessAsync(
				new List<IDomainEvent> { new TestOrderPlaced() },
				CreateContext(),
				NotificationFailurePolicy.Propagate,
				CancellationToken.None));

		ex.InnerExceptions.Count.ShouldBe(1);
		ex.InnerExceptions[0].ShouldBeOfType<InvalidOperationException>();
		ex.Message.ShouldContain("do NOT retry SaveAsync");
	}


	// THE MESSAGE IS THE CONSUMER'S ONLY POINTER TO THE REMEDY, and that is why it is asserted rather than
	// left to review. The superseded wording named IProjectionRecovery.ReapplyAsync as THE remedy; on a
	// store that records positions that call refuses terminally, because inline apply hard-codes
	// GlobalPosition: null and so leaves a row with no position a later write can advance from. The wrong
	// advice survived precisely because no arm read the text.
	[Fact]
	public async Task Send_a_position_recording_store_to_the_rebuild_rather_than_to_recovery()
	{
		// Arrange
		var registration = new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Inline,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (_, _, _, _) =>
				Task.FromException(new InvalidOperationException("store failure")));
		_registry.Register(registration);

		// Act
		var ex = await Should.ThrowAsync<AggregateException>(() =>
			_processor.ProcessAsync(
				new List<IDomainEvent> { new TestOrderPlaced() },
				CreateContext(),
				NotificationFailurePolicy.Propagate,
				CancellationToken.None));

		// Assert
		ex.Message.ShouldContain(
			"REBUILD",
			Case.Sensitive,
			"a consumer whose store records positions must be sent to the rebuild, which is the only "
			+ "repair that can succeed against a row inline apply produced");

		ex.Message.ShouldContain(
			"REFUSE",
			Case.Sensitive,
			"and must be told that the recovery call refuses such a row, so they do not spend a hop "
			+ "discovering it -- the framework used to send them there and recovery then sent them on");

		// NOT a bare "does it mention ReapplyAsync" check. The message names that call deliberately, for
		// the store which records NO positions, where it genuinely is the remedy. What must not come back
		// is presenting it as the remedy with no mention that it refuses the row inline apply leaves.
		ex.Message.ShouldNotContain(
			"Use IProjectionRecovery.ReapplyAsync to recover",
			Case.Sensitive,
			"the superseded wording presented the recovery call as THE remedy, unqualified");
	}


	// PINNED FOR THE SAME REASON AS THE THROW, and this is the branch a consumer reaches when they have NOT
	// opted into propagation -- arguably the more common one. The superseded text promised "projection will
	// catch up via async path", and that was false twice over: a projection is registered in exactly ONE
	// mode, so an Inline projection is never DELIVERED to the async host (the primary reason), and even if
	// it were, the async host writes POSITIONED against a row inline apply left with no position, so the
	// write would be refused (a redundancy, not the mechanism).
	//
	// It survived for exactly the reason the throw's wrong advice survived: nothing read the text. This arm
	// asserts CASE-SENSITIVELY on purpose -- a case-insensitive phrase assertion is what let a stale claim
	// about this same message pass unnoticed against both the old wording and its replacement.
	[Fact]
	public async Task Not_promise_an_automatic_catch_up_when_the_policy_is_log_and_continue()
	{
		// Arrange
		var logger = new CapturingLogger();
		var processor = new InlineProjectionProcessor(_registry, _scopeFactory, logger);

		var registration = new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Inline,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (_, _, _, _) =>
				Task.FromException(new InvalidOperationException("store failure")));
		_registry.Register(registration);

		// Act -- LogAndContinue does not throw, so the log IS the entire consumer-facing output.
		await processor.ProcessAsync(
			new List<IDomainEvent> { new TestOrderPlaced() },
			CreateContext(),
			NotificationFailurePolicy.LogAndContinue,
			CancellationToken.None);

		// Assert
		var logged = logger.Messages.ShouldHaveSingleItem();

		logged.ShouldNotContain(
			"catch up via async path",
			Case.Sensitive,
			"there is no async catch-up for an Inline projection, so promising one tells an operator the "
			+ "system will self-heal -- which makes doing nothing the correct response to the message, and "
			+ "nothing is then what happens");

		logged.ShouldContain(
			"NOT RECOVERED AUTOMATICALLY",
			Case.Sensitive,
			"the operator must be told the failure is theirs to repair, because nothing else will");

		logged.ShouldContain(
			"REBUILD",
			Case.Sensitive,
			"and told WHICH repair: a rebuild on a store that records positions, which is where an inline "
			+ "write leaves a row no later positioned write can advance from");
	}

	[Fact]
	public async Task LogAndContinueDoesNotThrow()
	{
		// Arrange
		var registration = new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Inline,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (_, _, _, _) =>
				Task.FromException(new InvalidOperationException("store failure")));
		_registry.Register(registration);

		// Act -- should NOT throw
		await _processor.ProcessAsync(
			new List<IDomainEvent> { new TestOrderPlaced() },
			CreateContext(),
			NotificationFailurePolicy.LogAndContinue,
			CancellationToken.None);
	}

	[Fact]
	public async Task RunMultipleProjectionsConcurrently()
	{
		// Arrange
		var projection1Completed = false;
		var projection2Completed = false;

		_registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Inline,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: async (_, _, _, _) =>
			{
				await Task.Yield(); // simulate async work
				projection1Completed = true;
			}));

		_registry.Register(new ProjectionRegistration(
			typeof(InventoryView),
			ProjectionMode.Inline,
			new MultiStreamProjection<InventoryView>(),
			inlineApply: async (_, _, _, _) =>
			{
				await Task.Yield();
				projection2Completed = true;
			}));

		// Act
		await _processor.ProcessAsync(
			new List<IDomainEvent> { new TestOrderPlaced() },
			CreateContext(),
			NotificationFailurePolicy.Propagate,
			CancellationToken.None);

		// Assert -- both ran
		projection1Completed.ShouldBeTrue();
		projection2Completed.ShouldBeTrue();
	}

	[Fact]
	public async Task PartialFailurePreservesSuccessfulWrites()
	{
		// Arrange -- one projection succeeds, one fails
		var successfulWritten = false;

		_registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Inline,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (_, _, _, _) =>
			{
				successfulWritten = true;
				return Task.CompletedTask;
			}));

		_registry.Register(new ProjectionRegistration(
			typeof(InventoryView),
			ProjectionMode.Inline,
			new MultiStreamProjection<InventoryView>(),
			inlineApply: (_, _, _, _) =>
				Task.FromException(new InvalidOperationException("inventory store down"))));

		// Act & Assert
		var ex = await Should.ThrowAsync<AggregateException>(() =>
			_processor.ProcessAsync(
				new List<IDomainEvent> { new TestOrderPlaced() },
				CreateContext(),
				NotificationFailurePolicy.Propagate,
				CancellationToken.None));

		// The successful write completed (R27.20a: partial failure)
		successfulWritten.ShouldBeTrue();
		ex.InnerExceptions.Count.ShouldBe(1);
	}

	[Fact]
	public async Task PassScopedServiceProviderToDelegate()
	{
		// Arrange -- the processor must hand the delegate a provider from a freshly created
		// scope (NOT a captured root provider), so scoped IProjectionStore<T> resolves.
		var scopedProvider = A.Fake<IServiceProvider>();
		var scope = A.Fake<IServiceScope>();
		A.CallTo(() => scope.ServiceProvider).Returns(scopedProvider);
		A.CallTo(() => _scopeFactory.CreateScope()).Returns(scope);

		IServiceProvider? capturedSp = null;
		_registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Inline,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (_, _, sp, _) =>
			{
				capturedSp = sp;
				return Task.CompletedTask;
			}));

		// Act
		await _processor.ProcessAsync(
			new List<IDomainEvent> { new TestOrderPlaced() },
			CreateContext(),
			NotificationFailurePolicy.Propagate,
			CancellationToken.None);

		// Assert -- delegate received the scope's provider.
		capturedSp.ShouldBeSameAs(scopedProvider);
	}

	[Fact]
	public async Task PassCancellationTokenToDelegate()
	{
		// Arrange
		CancellationToken capturedToken = default;
		using var cts = new CancellationTokenSource();

		_registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Inline,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (_, _, _, ct) =>
			{
				capturedToken = ct;
				return Task.CompletedTask;
			}));

		// Act
		await _processor.ProcessAsync(
			new List<IDomainEvent> { new TestOrderPlaced() },
			CreateContext(),
			NotificationFailurePolicy.Propagate,
			cts.Token);

		// Assert
		capturedToken.ShouldBe(cts.Token);
	}

	[Fact]
	public async Task RecordErrorInHealthStateOnProjectionFailure()
	{
		// Arrange -- processor with health state + observability
		var healthState = new ProjectionHealthState();
		using var meterFactory = new TestMeterFactory();
		var observability = new ProjectionObservability(meterFactory);

		var processor = new InlineProjectionProcessor(
			_registry,
			_scopeFactory,
			NullLogger<InlineProjectionProcessor>.Instance,
			healthState,
			observability);

		_registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Inline,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (_, _, _, _) =>
				Task.FromException(new InvalidOperationException("store failure"))));

		// Act -- LogAndContinue so it doesn't throw
		await processor.ProcessAsync(
			new List<IDomainEvent> { new TestOrderPlaced() },
			CreateContext(),
			NotificationFailurePolicy.LogAndContinue,
			CancellationToken.None);

		// Assert -- health state updated
		healthState.LastInlineError.ShouldNotBeNull();
		healthState.LastErrorProjectionType.ShouldBe(nameof(OrderSummary));
	}

	[Fact]
	public async Task NotRecordErrorWhenObservabilityIsNull()
	{
		// Arrange -- processor without observability (nullable params = null)
		var processor = new InlineProjectionProcessor(
			_registry,
			_scopeFactory,
			NullLogger<InlineProjectionProcessor>.Instance,
			healthState: null,
			observability: null);

		_registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Inline,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (_, _, _, _) =>
				Task.FromException(new InvalidOperationException("store failure"))));

		// Act -- should not throw NullReferenceException
		await processor.ProcessAsync(
			new List<IDomainEvent> { new TestOrderPlaced() },
			CreateContext(),
			NotificationFailurePolicy.LogAndContinue,
			CancellationToken.None);
	}

	private sealed class TestMeterFactory : IMeterFactory
	{
		private readonly List<Meter> _meters = [];

		public Meter Create(MeterOptions options)
		{
			var meter = new Meter(options);
			_meters.Add(meter);
			return meter;
		}

		public void Dispose()
		{
			foreach (var meter in _meters) meter.Dispose();
		}
	}

	/// <summary>
	/// Captures the FORMATTED log text, which is what a consumer reads.
	/// </summary>
	/// <remarks>
	/// A hand-written double rather than a mock: the message under test is produced by the formatter, so an
	/// assertion on the template or on the state object would pin something the operator never sees.
	/// </remarks>
	private sealed class CapturingLogger : ILogger<InlineProjectionProcessor>
	{
		private readonly List<string> _messages = [];

		public IReadOnlyList<string> Messages => _messages;

		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			Microsoft.Extensions.Logging.EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			ArgumentNullException.ThrowIfNull(formatter);
			_messages.Add(formatter(state, exception));
		}
	}

}
