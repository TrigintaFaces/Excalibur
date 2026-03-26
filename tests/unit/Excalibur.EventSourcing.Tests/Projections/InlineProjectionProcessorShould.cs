// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;
using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.Projections;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InlineProjectionProcessorShould
{
	private readonly InMemoryProjectionRegistry _registry = new();
	private readonly IServiceProvider _serviceProvider = A.Fake<IServiceProvider>();
	private readonly InlineProjectionProcessor _processor;

	public InlineProjectionProcessorShould()
	{
		_processor = new InlineProjectionProcessor(
			_registry,
			_serviceProvider,
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
	public async Task PassServiceProviderToDelegate()
	{
		// Arrange
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

		// Assert
		capturedSp.ShouldBeSameAs(_serviceProvider);
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
			_serviceProvider,
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
			_serviceProvider,
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
}
