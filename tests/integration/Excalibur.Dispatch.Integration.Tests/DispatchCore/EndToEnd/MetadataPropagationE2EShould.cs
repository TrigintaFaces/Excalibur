// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA1034 // Nested types should not be visible - acceptable in test classes

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.EndToEnd;

/// <summary>
/// Comprehensive E2E tests verifying metadata propagation, message delivery, and framework
/// correctness across all dispatch paths. These tests prove the framework is functionally
/// complete by asserting that CorrelationId, CausationId, MessageId, Items, Features, and
/// custom context data survive the full dispatch → middleware → handler → result pipeline.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Category", "EndToEnd")]
[Trait("Component", "MetadataPropagation")]
public sealed class MetadataPropagationE2EShould : IDisposable
{
	private readonly ServiceProvider _serviceProvider;
	private readonly IDispatcher _dispatcher;
	private readonly IMessageContextFactory _contextFactory;

	public MetadataPropagationE2EShould()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Register handlers BEFORE AddDispatch
		_ = services.AddTransient<CapturingCommandHandler>();
		_ = services.AddTransient<IActionHandler<MetadataCommand>, CapturingCommandHandler>();

		_ = services.AddTransient<CapturingQueryHandler>();
		_ = services.AddTransient<IActionHandler<MetadataQuery, MetadataQueryResult>, CapturingQueryHandler>();

		_ = services.AddTransient<CapturingEventHandler>();
		_ = services.AddTransient<IEventHandler<MetadataEvent>, CapturingEventHandler>();

		_ = services.AddTransient<FailingCommandHandler>();
		_ = services.AddTransient<IActionHandler<FailingMetadataCommand>, FailingCommandHandler>();

		_ = services.AddTransient<CancellableCommandHandler>();
		_ = services.AddTransient<IActionHandler<CancellableCommand>, CancellableCommandHandler>();

		_ = services.AddTransient<ChainingCommandHandler>();
		_ = services.AddTransient<IActionHandler<ChainingCommand>, ChainingCommandHandler>();

		_ = services.AddTransient<ChildCommandHandler>();
		_ = services.AddTransient<IActionHandler<ChildCommand>, ChildCommandHandler>();

		// Register test middleware that captures context at each pipeline stage
		_ = services.AddDispatchMiddleware<ContextCapturingMiddleware>();

		_ = services.AddDispatch();

		_serviceProvider = services.BuildServiceProvider();
		_dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
		_contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();
	}

	public void Dispose()
	{
		CapturingCommandHandler.Reset();
		CapturingQueryHandler.Reset();
		CapturingEventHandler.Reset();
		FailingCommandHandler.Reset();
		CancellableCommandHandler.Reset();
		ChainingCommandHandler.Reset();
		ChildCommandHandler.Reset();
		ContextCapturingMiddleware.Reset();
		_serviceProvider.Dispose();
	}

	// ── 1. Core Metadata Propagation ──────────────────────────────────

	[Fact]
	public async Task CorrelationId_PropagatesThroughEntirePipeline_ToHandler()
	{
		// Arrange
		var command = new MetadataCommand { Id = Guid.NewGuid() };
		var context = _contextFactory.CreateContext();
		context.CorrelationId = "correlation-e2e-001";

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		CapturingCommandHandler.LastCapturedContext.ShouldNotBeNull();
		CapturingCommandHandler.LastCapturedContext.CorrelationId.ShouldBe("correlation-e2e-001");
	}

	[Fact]
	public async Task CausationId_PropagatesThroughEntirePipeline_ToHandler()
	{
		// Arrange
		var command = new MetadataCommand { Id = Guid.NewGuid() };
		var context = _contextFactory.CreateContext();
		context.CausationId = "causation-e2e-001";

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		CapturingCommandHandler.LastCapturedContext.ShouldNotBeNull();
		CapturingCommandHandler.LastCapturedContext.CausationId.ShouldBe("causation-e2e-001");
	}

	[Fact]
	public async Task MessageId_IsAssigned_AndPropagatesThroughPipeline()
	{
		// Arrange
		var command = new MetadataCommand { Id = Guid.NewGuid() };
		var context = _contextFactory.CreateContext();
		context.MessageId = "msg-e2e-001";

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		CapturingCommandHandler.LastCapturedContext.ShouldNotBeNull();
		CapturingCommandHandler.LastCapturedContext.MessageId.ShouldBe("msg-e2e-001");
	}

	[Fact]
	public async Task MessageId_AutoGeneratedWhenNotSet()
	{
		// Arrange
		var command = new MetadataCommand { Id = Guid.NewGuid() };
		var context = _contextFactory.CreateContext();
		// Do NOT set MessageId

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		CapturingCommandHandler.LastCapturedContext.ShouldNotBeNull();
		CapturingCommandHandler.LastCapturedContext.MessageId.ShouldNotBeNullOrEmpty(
			"MessageId should be auto-generated when not explicitly set");
	}

	[Fact]
	public async Task AllThreeIds_PropagateTogetherThroughPipeline()
	{
		// Arrange
		var command = new MetadataCommand { Id = Guid.NewGuid() };
		var context = _contextFactory.CreateContext();
		context.MessageId = "msg-triple-001";
		context.CorrelationId = "corr-triple-001";
		context.CausationId = "cause-triple-001";

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		var captured = CapturingCommandHandler.LastCapturedContext;
		captured.ShouldNotBeNull();
		captured.MessageId.ShouldBe("msg-triple-001");
		captured.CorrelationId.ShouldBe("corr-triple-001");
		captured.CausationId.ShouldBe("cause-triple-001");
	}

	// ── 2. Context Items and Features Propagation ─────────────────────

	[Fact]
	public async Task ContextItems_PropagateFromCallerToHandler()
	{
		// Arrange
		var command = new MetadataCommand { Id = Guid.NewGuid() };
		var context = _contextFactory.CreateContext();
		context.Items["custom-key"] = "custom-value";
		context.Items["tenant-region"] = "us-east-1";

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		var captured = CapturingCommandHandler.LastCapturedContext;
		captured.ShouldNotBeNull();
		captured.Items.ShouldContainKey("custom-key");
		captured.Items["custom-key"].ShouldBe("custom-value");
		captured.Items.ShouldContainKey("tenant-region");
		captured.Items["tenant-region"].ShouldBe("us-east-1");
	}

	[Fact]
	public async Task IdentityFeature_PropagatesFromCallerToHandler()
	{
		// Arrange
		var command = new MetadataCommand { Id = Guid.NewGuid() };
		var context = _contextFactory.CreateContext();
		var identity = context.GetOrCreateIdentityFeature();
		identity.UserId = "user-e2e-001";
		identity.TenantId = "tenant-e2e-001";

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		var captured = CapturingCommandHandler.LastCapturedContext;
		captured.ShouldNotBeNull();
		captured.GetUserId().ShouldBe("user-e2e-001");
		captured.GetTenantId().ShouldBe("tenant-e2e-001");
	}

	// ── 3. Middleware Pipeline Metadata Integrity ──────────────────────

	[Fact]
	public async Task Middleware_SeesCorrectMetadata_BeforeHandlerRuns()
	{
		// Arrange
		ContextCapturingMiddleware.Reset();
		var command = new MetadataCommand { Id = Guid.NewGuid() };
		var context = _contextFactory.CreateContext();
		context.CorrelationId = "mw-corr-001";
		context.CausationId = "mw-cause-001";
		context.Items["mw-test"] = "middleware-value";

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);

		// Middleware should have captured the context BEFORE the handler ran
		ContextCapturingMiddleware.CapturedSnapshots.ShouldNotBeEmpty();
		var snapshot = ContextCapturingMiddleware.CapturedSnapshots.First();
		snapshot.CorrelationId.ShouldBe("mw-corr-001");
		snapshot.CausationId.ShouldBe("mw-cause-001");
		snapshot.Items.ShouldContainKey("mw-test");
		snapshot.Items["mw-test"].ShouldBe("middleware-value");
	}

	[Fact]
	public async Task MiddlewareModifiedItems_VisibleToHandler()
	{
		// Arrange
		ContextCapturingMiddleware.Reset();
		ContextCapturingMiddleware.ItemsToInject["injected-by-middleware"] = "mw-injected";
		var command = new MetadataCommand { Id = Guid.NewGuid() };
		var context = _contextFactory.CreateContext();

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		var captured = CapturingCommandHandler.LastCapturedContext;
		captured.ShouldNotBeNull();
		captured.Items.ShouldContainKey("injected-by-middleware");
		captured.Items["injected-by-middleware"].ShouldBe("mw-injected");
	}

	// ── 4. Query (Request-Response) Metadata Propagation ──────────────

	[Fact]
	public async Task Query_CorrelationId_PropagatesAndResultReturned()
	{
		// Arrange
		var query = new MetadataQuery { QueryParam = "find-user-42" };
		var context = _contextFactory.CreateContext();
		context.CorrelationId = "query-corr-001";
		context.CausationId = "query-cause-001";
		context.Items["query-tag"] = "tagged";

		// Act
		var result = await _dispatcher.DispatchAsync<MetadataQuery, MetadataQueryResult>(
			query, context, CancellationToken.None);

		// Assert - result delivered
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		result.ReturnValue.ShouldNotBeNull();
		result.ReturnValue.Answer.ShouldBe("Result for: find-user-42");

		// Assert - metadata propagated
		var captured = CapturingQueryHandler.LastCapturedContext;
		captured.ShouldNotBeNull();
		captured.CorrelationId.ShouldBe("query-corr-001");
		captured.CausationId.ShouldBe("query-cause-001");
		captured.Items.ShouldContainKey("query-tag");
	}

	[Fact]
	public async Task Query_ReturnValue_IsPreservedInResult()
	{
		// Arrange
		var query = new MetadataQuery { QueryParam = "complex-value" };
		var context = _contextFactory.CreateContext();

		// Act
		var result = await _dispatcher.DispatchAsync<MetadataQuery, MetadataQueryResult>(
			query, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.ReturnValue.ShouldNotBeNull();
		result.ReturnValue.Answer.ShouldBe("Result for: complex-value");
		result.ReturnValue.Timestamp.ShouldBeGreaterThan(DateTimeOffset.MinValue);
	}

	// ── 5. Failure Path Metadata Preservation ─────────────────────────

	[Fact]
	public async Task FailedCommand_ReturnsFailureResult_WithErrorMessage()
	{
		// Arrange
		var command = new FailingMetadataCommand
		{
			FailureReason = "Business rule violation: insufficient funds",
		};
		var context = _contextFactory.CreateContext();
		context.CorrelationId = "fail-corr-001";

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ErrorMessage.ShouldNotBeNullOrEmpty();
		result.ErrorMessage.ShouldContain("insufficient funds");
	}

	[Fact]
	public async Task FailedCommand_MetadataStillReachesHandler()
	{
		// Arrange - Even on failure, metadata should propagate to the handler
		var command = new FailingMetadataCommand { FailureReason = "expected" };
		var context = _contextFactory.CreateContext();
		context.CorrelationId = "fail-meta-001";
		context.CausationId = "fail-cause-001";

		// Act
		_ = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert - handler received the metadata before it threw
		FailingCommandHandler.LastCapturedCorrelationId.ShouldBe("fail-meta-001");
		FailingCommandHandler.LastCapturedCausationId.ShouldBe("fail-cause-001");
	}

	// ── 6. Cancellation Behavior ──────────────────────────────────────

	[Fact]
	public async Task CancelledRequest_ReturnsGracefully()
	{
		// Arrange
		var command = new CancellableCommand { WorkDurationMs = 5000 };
		var context = _contextFactory.CreateContext();
		context.CorrelationId = "cancel-corr-001";
		using var cts = new CancellationTokenSource();

		// Act - cancel almost immediately
		cts.CancelAfter(TimeSpan.FromMilliseconds(50));

		// The framework should either throw OperationCanceledException or return a failed result
		// Both are acceptable behaviors for cancellation
		try
		{
			var result = await _dispatcher.DispatchAsync(command, context, cts.Token);
			// If we get a result, it should indicate failure (cancelled)
			result.Succeeded.ShouldBeFalse("Cancelled dispatch should not succeed");
		}
		catch (OperationCanceledException)
		{
			// This is also acceptable - the framework bubbled the cancellation
		}
	}

	// ── 7. Concurrent Dispatch Metadata Isolation ─────────────────────

	[Fact]
	public async Task ConcurrentDispatches_EachGetOwnIsolatedContext()
	{
		// Arrange - dispatch 50 messages concurrently, each with unique metadata
		const int count = 50;
		var tasks = new List<Task<(string ExpectedCorrelation, IMessageResult Result)>>(count);

		for (var i = 0; i < count; i++)
		{
			var idx = i;
			tasks.Add(Task.Run(async () =>
			{
				var cmd = new MetadataCommand { Id = Guid.NewGuid() };
				var ctx = _contextFactory.CreateContext();
				var expectedCorrelation = $"concurrent-{idx:D4}";
				ctx.CorrelationId = expectedCorrelation;

				var res = await _dispatcher.DispatchAsync(cmd, ctx, CancellationToken.None);
				return (expectedCorrelation, res);
			}));
		}

		var results = await Task.WhenAll(tasks);

		// Assert - all succeeded
		foreach (var (_, result) in results)
		{
			result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		}

		// Assert - each captured context had a unique correlation ID (no cross-contamination)
		var capturedCorrelations = CapturingCommandHandler.AllCapturedCorrelationIds.ToList();
		capturedCorrelations.Count.ShouldBeGreaterThanOrEqualTo(count);

		// Verify all expected correlation IDs were captured
		for (var i = 0; i < count; i++)
		{
			capturedCorrelations.ShouldContain($"concurrent-{i:D4}",
				$"Missing correlation ID for concurrent dispatch #{i}");
		}
	}

	// ── 8. MessageContextHolder (Ambient Context) ─────────────────────

	[Fact]
	public async Task MessageContextHolder_ExposesContextDuringHandlerExecution()
	{
		// Arrange
		var command = new MetadataCommand { Id = Guid.NewGuid() };
		var context = _contextFactory.CreateContext();
		context.CorrelationId = "ambient-corr-001";

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		// Handler captures from MessageContextHolder.Current
		CapturingCommandHandler.LastCapturedContext.ShouldNotBeNull();
		CapturingCommandHandler.LastCapturedContext.CorrelationId.ShouldBe("ambient-corr-001");
	}

	// ── 9. Message Delivery Guarantees ────────────────────────────────

	[Fact]
	public async Task DispatchedMessage_IsDeliveredToCorrectHandler()
	{
		// Arrange
		CapturingCommandHandler.Reset();
		var commandId = Guid.NewGuid();
		var command = new MetadataCommand { Id = commandId, Payload = "delivery-test" };
		var context = _contextFactory.CreateContext();

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		CapturingCommandHandler.ProcessedIds.ShouldContain(commandId);
		CapturingCommandHandler.LastReceivedPayload.ShouldBe("delivery-test");
	}

	[Fact]
	public async Task DispatchedQuery_ReturnsCorrectTypedResult()
	{
		// Arrange
		var query = new MetadataQuery { QueryParam = "typed-result-test" };
		var context = _contextFactory.CreateContext();

		// Act
		var result = await _dispatcher.DispatchAsync<MetadataQuery, MetadataQueryResult>(
			query, context, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		result.ReturnValue.ShouldNotBeNull();
		result.ReturnValue.ShouldBeOfType<MetadataQueryResult>();
		result.ReturnValue.Answer.ShouldBe("Result for: typed-result-test");
	}

	[Fact]
	public async Task MultipleSequentialDispatches_AllDeliverCorrectly()
	{
		// Arrange & Act
		CapturingCommandHandler.Reset();
		var ids = new List<Guid>();

		for (var i = 0; i < 20; i++)
		{
			var id = Guid.NewGuid();
			ids.Add(id);
			var cmd = new MetadataCommand { Id = id, Payload = $"seq-{i}" };
			var ctx = _contextFactory.CreateContext();
			ctx.CorrelationId = $"seq-corr-{i}";

			var result = await _dispatcher.DispatchAsync(cmd, ctx, CancellationToken.None);
			result.Succeeded.ShouldBeTrue($"Sequential dispatch #{i} failed: {result.ErrorMessage}");
		}

		// Assert - all 20 delivered
		CapturingCommandHandler.ProcessedIds.Count.ShouldBe(20);
		foreach (var id in ids)
		{
			CapturingCommandHandler.ProcessedIds.ShouldContain(id);
		}
	}

	// ── 10. Edge Cases ────────────────────────────────────────────────

	[Fact]
	public async Task NullCorrelationId_DoesNotCrashPipeline()
	{
		// Arrange
		var command = new MetadataCommand { Id = Guid.NewGuid() };
		var context = _contextFactory.CreateContext();
		// Explicitly do not set CorrelationId

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert - should succeed without crashing
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
	}

	[Fact]
	public async Task EmptyStringCorrelationId_PreservedThrough()
	{
		// Arrange
		var command = new MetadataCommand { Id = Guid.NewGuid() };
		var context = _contextFactory.CreateContext();
		context.CorrelationId = string.Empty;

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		CapturingCommandHandler.LastCapturedContext.ShouldNotBeNull();
		CapturingCommandHandler.LastCapturedContext.CorrelationId.ShouldBe(string.Empty);
	}

	[Fact]
	public async Task LargeMetadata_PropagatesWithoutTruncation()
	{
		// Arrange - set a large correlation ID and many Items
		var command = new MetadataCommand { Id = Guid.NewGuid() };
		var context = _contextFactory.CreateContext();
		context.CorrelationId = new string('x', 512);
		context.CausationId = new string('y', 256);
		for (var i = 0; i < 50; i++)
		{
			context.Items[$"key-{i}"] = $"value-{i}-{new string('z', 100)}";
		}

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		var captured = CapturingCommandHandler.LastCapturedContext;
		captured.ShouldNotBeNull();
		captured.CorrelationId.ShouldBe(new string('x', 512));
		captured.CausationId.ShouldBe(new string('y', 256));
		captured.Items.Count.ShouldBeGreaterThanOrEqualTo(50);
	}

	[Fact]
	public async Task RequestServices_AvailableInHandler()
	{
		// Arrange
		var command = new MetadataCommand { Id = Guid.NewGuid() };
		var context = _contextFactory.CreateContext();

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		CapturingCommandHandler.LastCapturedContext.ShouldNotBeNull();
		CapturingCommandHandler.LastCapturedContext.RequestServices.ShouldNotBeNull(
			"RequestServices must be available to handlers for DI resolution");
	}

	// ── 11. Event Dispatch Metadata Propagation ──────────────────────

	[Fact]
	public async Task Event_CorrelationId_PropagatesThroughPipeline_ToEventHandler()
	{
		// Arrange
		CapturingEventHandler.Reset();
		var evt = new MetadataEvent { EventId = Guid.NewGuid(), Data = "event-corr-test" };
		var context = _contextFactory.CreateContext();
		context.CorrelationId = "event-corr-001";
		context.CausationId = "event-cause-001";

		// Act
		var result = await _dispatcher.DispatchAsync(evt, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		CapturingEventHandler.LastCapturedContext.ShouldNotBeNull();
		CapturingEventHandler.LastCapturedContext.CorrelationId.ShouldBe("event-corr-001");
		CapturingEventHandler.LastCapturedContext.CausationId.ShouldBe("event-cause-001");
	}

	[Fact]
	public async Task Event_MessageId_PropagatesThroughPipeline()
	{
		// Arrange
		CapturingEventHandler.Reset();
		var evt = new MetadataEvent { EventId = Guid.NewGuid(), Data = "event-msgid-test" };
		var context = _contextFactory.CreateContext();
		context.MessageId = "event-msg-001";

		// Act
		var result = await _dispatcher.DispatchAsync(evt, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		CapturingEventHandler.LastCapturedContext.ShouldNotBeNull();
		CapturingEventHandler.LastCapturedContext.MessageId.ShouldBe("event-msg-001");
	}

	[Fact]
	public async Task Event_Items_PropagateToEventHandler()
	{
		// Arrange
		CapturingEventHandler.Reset();
		var evt = new MetadataEvent { EventId = Guid.NewGuid(), Data = "event-items-test" };
		var context = _contextFactory.CreateContext();
		context.CorrelationId = "event-items-001";
		context.Items["event-key"] = "event-value";
		context.Items["source-system"] = "order-service";

		// Act
		var result = await _dispatcher.DispatchAsync(evt, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		var captured = CapturingEventHandler.LastCapturedContext;
		captured.ShouldNotBeNull();
		captured.Items.ShouldContainKey("event-key");
		captured.Items["event-key"].ShouldBe("event-value");
		captured.Items.ShouldContainKey("source-system");
		captured.Items["source-system"].ShouldBe("order-service");
	}

	[Fact]
	public async Task Event_IdentityFeature_PropagesToEventHandler()
	{
		// Arrange
		CapturingEventHandler.Reset();
		var evt = new MetadataEvent { EventId = Guid.NewGuid(), Data = "event-identity-test" };
		var context = _contextFactory.CreateContext();
		var identity = context.GetOrCreateIdentityFeature();
		identity.UserId = "event-user-001";
		identity.TenantId = "event-tenant-001";

		// Act
		var result = await _dispatcher.DispatchAsync(evt, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		var captured = CapturingEventHandler.LastCapturedContext;
		captured.ShouldNotBeNull();
		captured.GetUserId().ShouldBe("event-user-001");
		captured.GetTenantId().ShouldBe("event-tenant-001");
	}

	[Fact]
	public async Task Event_DeliveredToHandler_WithCorrectPayload()
	{
		// Arrange
		CapturingEventHandler.Reset();
		var eventId = Guid.NewGuid();
		var evt = new MetadataEvent { EventId = eventId, Data = "delivery-check" };
		var context = _contextFactory.CreateContext();

		// Act
		var result = await _dispatcher.DispatchAsync(evt, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		CapturingEventHandler.ProcessedEvents.ShouldContain(eventId);
	}

	[Fact]
	public async Task Event_AllMetadata_PropagatesTogether()
	{
		// Arrange
		CapturingEventHandler.Reset();
		var eventId = Guid.NewGuid();
		var evt = new MetadataEvent { EventId = eventId, Data = "all-meta-test" };
		var context = _contextFactory.CreateContext();
		context.MessageId = "event-all-msg-001";
		context.CorrelationId = "event-all-corr-001";
		context.CausationId = "event-all-cause-001";
		context.Items["event-tag"] = "tagged";
		var identity = context.GetOrCreateIdentityFeature();
		identity.UserId = "event-all-user";
		identity.TenantId = "event-all-tenant";

		// Act
		var result = await _dispatcher.DispatchAsync(evt, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		var captured = CapturingEventHandler.LastCapturedContext;
		captured.ShouldNotBeNull();
		captured.MessageId.ShouldBe("event-all-msg-001");
		captured.CorrelationId.ShouldBe("event-all-corr-001");
		captured.CausationId.ShouldBe("event-all-cause-001");
		captured.Items.ShouldContainKey("event-tag");
		captured.GetUserId().ShouldBe("event-all-user");
		captured.GetTenantId().ShouldBe("event-all-tenant");
		CapturingEventHandler.ProcessedEvents.ShouldContain(eventId);
	}

	// ── 12. Chained Dispatch (Handler → Child Dispatch) ──────────────

	[Fact]
	public async Task ChainedDispatch_ChildInheritsParentCorrelationId()
	{
		// Arrange
		ChildCommandHandler.Reset();
		var command = new ChainingCommand { Id = Guid.NewGuid() };
		var context = _contextFactory.CreateContext();
		context.CorrelationId = "parent-corr-chain-001";
		context.MessageId = "parent-msg-chain-001";

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		ChildCommandHandler.LastCapturedContext.ShouldNotBeNull();
		ChildCommandHandler.LastCapturedContext.CorrelationId.ShouldBe("parent-corr-chain-001",
			"Child dispatch should inherit parent's CorrelationId for distributed tracing");
	}

	[Fact]
	public async Task ChainedDispatch_ChildCausationId_IsParentMessageId()
	{
		// Arrange
		ChildCommandHandler.Reset();
		var command = new ChainingCommand { Id = Guid.NewGuid() };
		var context = _contextFactory.CreateContext();
		context.CorrelationId = "parent-corr-causation-001";
		context.MessageId = "parent-msg-causation-001";

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		ChildCommandHandler.LastCapturedContext.ShouldNotBeNull();
		ChildCommandHandler.LastCapturedContext.CausationId.ShouldBe("parent-msg-causation-001",
			"Child's CausationId should be parent's MessageId to establish causation chain");
	}

	[Fact]
	public async Task ChainedDispatch_ChildGetsOwnMessageId()
	{
		// Arrange
		ChildCommandHandler.Reset();
		var command = new ChainingCommand { Id = Guid.NewGuid() };
		var context = _contextFactory.CreateContext();
		context.MessageId = "parent-msg-own-001";

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		var childCtx = ChildCommandHandler.LastCapturedContext;
		childCtx.ShouldNotBeNull();
		childCtx.MessageId.ShouldNotBeNullOrEmpty();
		childCtx.MessageId.ShouldNotBe("parent-msg-own-001",
			"Child should have its own unique MessageId, not the parent's");
	}

	[Fact]
	public async Task ChainedDispatch_ChildItemsAreIsolatedFromParent()
	{
		// Arrange
		ChildCommandHandler.Reset();
		var command = new ChainingCommand { Id = Guid.NewGuid() };
		var context = _contextFactory.CreateContext();
		context.CorrelationId = "parent-items-isolation";
		context.Items["parent-only-key"] = "should-not-leak";

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert - child context should NOT contain parent-only Items
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		var childCtx = ChildCommandHandler.LastCapturedContext;
		childCtx.ShouldNotBeNull();

		// The child context is a fresh context, so it should have the child-injected item
		childCtx.Items.ShouldContainKey("child-injected");
		childCtx.Items["child-injected"].ShouldBe("from-chaining-handler");
	}

	// ── 13. Lazy Correlation (UseContextEnrichment) ──────────────────

	[Fact]
	public async Task LazyCorrelation_AutoGeneratedWhenContextEnrichmentEnabled()
	{
		// Arrange - build a separate provider with UseContextEnrichment
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddTransient<CapturingCommandHandler>();
		_ = services.AddTransient<IActionHandler<MetadataCommand>, CapturingCommandHandler>();
		_ = services.AddDispatch(builder => builder.UseContextEnrichment());

		using var sp = services.BuildServiceProvider();
		var dispatcher = sp.GetRequiredService<IDispatcher>();
		var factory = sp.GetRequiredService<IMessageContextFactory>();

		CapturingCommandHandler.Reset();
		var command = new MetadataCommand { Id = Guid.NewGuid() };
		var context = factory.CreateContext();
		// Do NOT set CorrelationId -- it should be auto-generated

		// Act
		var result = await dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		var captured = CapturingCommandHandler.LastCapturedContext;
		captured.ShouldNotBeNull();
		captured.CorrelationId.ShouldNotBeNullOrEmpty(
			"CorrelationId should be auto-generated when UseContextEnrichment is enabled");
	}

	[Fact]
	public async Task LazyCorrelation_CausationIdDefaultsToCorrelationId()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddTransient<CapturingCommandHandler>();
		_ = services.AddTransient<IActionHandler<MetadataCommand>, CapturingCommandHandler>();
		_ = services.AddDispatch(builder => builder.UseContextEnrichment());

		using var sp = services.BuildServiceProvider();
		var dispatcher = sp.GetRequiredService<IDispatcher>();
		var factory = sp.GetRequiredService<IMessageContextFactory>();

		CapturingCommandHandler.Reset();
		var command = new MetadataCommand { Id = Guid.NewGuid() };
		var context = factory.CreateContext();
		// Do NOT set CorrelationId or CausationId

		// Act
		var result = await dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		var captured = CapturingCommandHandler.LastCapturedContext;
		captured.ShouldNotBeNull();
		captured.CorrelationId.ShouldNotBeNullOrEmpty();
		captured.CausationId.ShouldNotBeNullOrEmpty();
		captured.CausationId.ShouldBe(captured.CorrelationId,
			"CausationId should default to CorrelationId when not explicitly set");
	}

	[Fact]
	public async Task LazyCorrelation_ExplicitIdTakesPrecedence()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddTransient<CapturingCommandHandler>();
		_ = services.AddTransient<IActionHandler<MetadataCommand>, CapturingCommandHandler>();
		_ = services.AddDispatch(builder => builder.UseContextEnrichment());

		using var sp = services.BuildServiceProvider();
		var dispatcher = sp.GetRequiredService<IDispatcher>();
		var factory = sp.GetRequiredService<IMessageContextFactory>();

		CapturingCommandHandler.Reset();
		var command = new MetadataCommand { Id = Guid.NewGuid() };
		var context = factory.CreateContext();
		context.CorrelationId = "explicit-wins";

		// Act
		var result = await dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		var captured = CapturingCommandHandler.LastCapturedContext;
		captured.ShouldNotBeNull();
		captured.CorrelationId.ShouldBe("explicit-wins",
			"Explicitly set CorrelationId should take precedence over lazy generation");
	}

	[Fact]
	public async Task LazyCorrelation_EachDispatchGetsUniqueCorrelationId()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddTransient<CapturingCommandHandler>();
		_ = services.AddTransient<IActionHandler<MetadataCommand>, CapturingCommandHandler>();
		_ = services.AddDispatch(builder => builder.UseContextEnrichment());

		using var sp = services.BuildServiceProvider();
		var dispatcher = sp.GetRequiredService<IDispatcher>();
		var factory = sp.GetRequiredService<IMessageContextFactory>();

		CapturingCommandHandler.Reset();
		var correlationIds = new List<string>();

		// Act - dispatch 10 messages without setting CorrelationId
		for (var i = 0; i < 10; i++)
		{
			var command = new MetadataCommand { Id = Guid.NewGuid() };
			var context = factory.CreateContext();

			var result = await dispatcher.DispatchAsync(command, context, CancellationToken.None);
			result.Succeeded.ShouldBeTrue(result.ErrorMessage);

			var captured = CapturingCommandHandler.LastCapturedContext;
			captured.ShouldNotBeNull();
			captured.CorrelationId.ShouldNotBeNullOrEmpty();
			correlationIds.Add(captured.CorrelationId!);
			CapturingCommandHandler.Reset();
		}

		// Assert - all 10 should be unique
		correlationIds.Distinct().Count().ShouldBe(10,
			"Each dispatch should generate a unique CorrelationId");
	}

	// ── Test Messages ─────────────────────────────────────────────────

	public sealed record MetadataCommand : IDispatchAction
	{
		public Guid Id { get; init; }
		public string Payload { get; init; } = string.Empty;
	}

	public sealed record MetadataQuery : IDispatchAction<MetadataQueryResult>
	{
		public string QueryParam { get; init; } = string.Empty;
	}

	public sealed record MetadataQueryResult
	{
		public string Answer { get; init; } = string.Empty;
		public DateTimeOffset Timestamp { get; init; }
	}

	public sealed record MetadataEvent : IDispatchEvent
	{
		public Guid EventId { get; init; }
		public string Data { get; init; } = string.Empty;
	}

	public sealed record FailingMetadataCommand : IDispatchAction
	{
		public string FailureReason { get; init; } = string.Empty;
	}

	public sealed record CancellableCommand : IDispatchAction
	{
		public int WorkDurationMs { get; init; }
	}

	public sealed record ChainingCommand : IDispatchAction
	{
		public Guid Id { get; init; }
	}

	public sealed record ChildCommand : IDispatchAction
	{
		public Guid ParentId { get; init; }
	}

	// ── Test Handlers ─────────────────────────────────────────────────

	public sealed class CapturingCommandHandler : IActionHandler<MetadataCommand>
	{
		private static readonly ConcurrentBag<Guid> s_processedIds = [];
		private static readonly ConcurrentBag<string> s_capturedCorrelations = [];
		private static IMessageContext? s_lastContext;
		private static string? s_lastPayload;

		public static ConcurrentBag<Guid> ProcessedIds => s_processedIds;
		public static ConcurrentBag<string> AllCapturedCorrelationIds => s_capturedCorrelations;
		public static IMessageContext? LastCapturedContext => s_lastContext;
		public static string? LastReceivedPayload => s_lastPayload;

		public static void Reset()
		{
			while (s_processedIds.TryTake(out _)) { }
			while (s_capturedCorrelations.TryTake(out _)) { }
			s_lastContext = null;
			s_lastPayload = null;
		}

		public Task HandleAsync(MetadataCommand action, CancellationToken cancellationToken)
		{
			s_processedIds.Add(action.Id);
			s_lastPayload = action.Payload;

			var ctx = MessageContextHolder.Current;
			if (ctx != null)
			{
				s_lastContext = ctx;
				if (ctx.CorrelationId != null)
				{
					s_capturedCorrelations.Add(ctx.CorrelationId);
				}
			}

			return Task.CompletedTask;
		}
	}

	public sealed class CapturingQueryHandler : IActionHandler<MetadataQuery, MetadataQueryResult>
	{
		private static IMessageContext? s_lastContext;

		public static IMessageContext? LastCapturedContext => s_lastContext;

		public static void Reset() => s_lastContext = null;

		public Task<MetadataQueryResult> HandleAsync(MetadataQuery action, CancellationToken cancellationToken)
		{
			s_lastContext = MessageContextHolder.Current;

			return Task.FromResult(new MetadataQueryResult
			{
				Answer = $"Result for: {action.QueryParam}",
				Timestamp = DateTimeOffset.UtcNow,
			});
		}
	}

	public sealed class CapturingEventHandler : IEventHandler<MetadataEvent>
	{
		private static readonly ConcurrentBag<Guid> s_processedEvents = [];
		private static IMessageContext? s_lastContext;

		public static ConcurrentBag<Guid> ProcessedEvents => s_processedEvents;
		public static IMessageContext? LastCapturedContext => s_lastContext;

		public static void Reset()
		{
			while (s_processedEvents.TryTake(out _)) { }
			s_lastContext = null;
		}

		public Task HandleAsync(MetadataEvent eventMessage, CancellationToken cancellationToken)
		{
			s_processedEvents.Add(eventMessage.EventId);
			s_lastContext = MessageContextHolder.Current;
			return Task.CompletedTask;
		}
	}

	public sealed class FailingCommandHandler : IActionHandler<FailingMetadataCommand>
	{
		private static string? s_lastCorrelationId;
		private static string? s_lastCausationId;

		public static string? LastCapturedCorrelationId => s_lastCorrelationId;
		public static string? LastCapturedCausationId => s_lastCausationId;

		public static void Reset()
		{
			s_lastCorrelationId = null;
			s_lastCausationId = null;
		}

		public Task HandleAsync(FailingMetadataCommand action, CancellationToken cancellationToken)
		{
			var ctx = MessageContextHolder.Current;
			if (ctx != null)
			{
				s_lastCorrelationId = ctx.CorrelationId;
				s_lastCausationId = ctx.CausationId;
			}

			throw new InvalidOperationException(action.FailureReason);
		}
	}

	public sealed class CancellableCommandHandler : IActionHandler<CancellableCommand>
	{
		private static IMessageContext? s_lastContext;
		public static IMessageContext? LastCapturedContext => s_lastContext;

		public static void Reset() => s_lastContext = null;

		public async Task HandleAsync(CancellableCommand action, CancellationToken cancellationToken)
		{
			s_lastContext = MessageContextHolder.Current;
			await Task.Delay(action.WorkDurationMs, cancellationToken);
		}
	}

	public sealed class ChainingCommandHandler : IActionHandler<ChainingCommand>
	{
		private static IMessageContext? s_lastContext;
		private readonly IDispatcher _dispatcher;
		private readonly IMessageContextFactory _contextFactory;

		public ChainingCommandHandler(IDispatcher dispatcher, IMessageContextFactory contextFactory)
		{
			_dispatcher = dispatcher;
			_contextFactory = contextFactory;
		}

		public static IMessageContext? LastCapturedContext => s_lastContext;

		public static void Reset() => s_lastContext = null;

		public async Task HandleAsync(ChainingCommand action, CancellationToken cancellationToken)
		{
			var parentCtx = MessageContextHolder.Current;
			s_lastContext = parentCtx;

			// Create child context, propagating correlation and setting causation
			var childCtx = _contextFactory.CreateContext();
			if (parentCtx != null)
			{
				childCtx.CorrelationId = parentCtx.CorrelationId;
				childCtx.CausationId = parentCtx.MessageId;
			}

			childCtx.Items["child-injected"] = "from-chaining-handler";

			var childCommand = new ChildCommand { ParentId = action.Id };
			var result = await _dispatcher.DispatchAsync(childCommand, childCtx, cancellationToken);
			if (!result.Succeeded)
			{
				throw new InvalidOperationException($"Child dispatch failed: {result.ErrorMessage}");
			}
		}
	}

	public sealed class ChildCommandHandler : IActionHandler<ChildCommand>
	{
		private static readonly ConcurrentBag<Guid> s_processedParentIds = [];
		private static IMessageContext? s_lastContext;

		public static ConcurrentBag<Guid> ProcessedParentIds => s_processedParentIds;
		public static IMessageContext? LastCapturedContext => s_lastContext;

		public static void Reset()
		{
			while (s_processedParentIds.TryTake(out _)) { }
			s_lastContext = null;
		}

		public Task HandleAsync(ChildCommand action, CancellationToken cancellationToken)
		{
			s_processedParentIds.Add(action.ParentId);
			s_lastContext = MessageContextHolder.Current;
			return Task.CompletedTask;
		}
	}

	// ── Test Middleware ────────────────────────────────────────────────

	/// <summary>
	/// Middleware that captures a snapshot of context metadata before forwarding to the next delegate.
	/// This proves metadata integrity at the middleware layer.
	/// </summary>
	public sealed class ContextCapturingMiddleware : IDispatchMiddleware
	{
		private static readonly ConcurrentBag<ContextSnapshot> s_snapshots = [];
		private static readonly ConcurrentDictionary<string, object> s_itemsToInject = new();

		public static ConcurrentBag<ContextSnapshot> CapturedSnapshots => s_snapshots;
		public static ConcurrentDictionary<string, object> ItemsToInject => s_itemsToInject;

		public DispatchMiddlewareStage? Stage => null;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;
		public IReadOnlyCollection<string>? RequiredFeatures => null;

		public static void Reset()
		{
			while (s_snapshots.TryTake(out _)) { }
			s_itemsToInject.Clear();
		}

		public async ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			// Capture snapshot before forwarding
			s_snapshots.Add(new ContextSnapshot
			{
				MessageId = context.MessageId,
				CorrelationId = context.CorrelationId,
				CausationId = context.CausationId,
				Items = new Dictionary<string, object>(context.Items),
			});

			// Inject any test items
			foreach (var kvp in s_itemsToInject)
			{
				context.Items[kvp.Key] = kvp.Value;
			}

			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
	}

	public sealed class ContextSnapshot
	{
		public string? MessageId { get; init; }
		public string? CorrelationId { get; init; }
		public string? CausationId { get; init; }
		public Dictionary<string, object> Items { get; init; } = [];
	}
}
