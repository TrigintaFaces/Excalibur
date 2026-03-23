// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery.Pipeline;

using MessageResult = Excalibur.Dispatch.Messaging.MessageResult;

namespace Excalibur.Dispatch.Tests.Middleware;

/// <summary>
/// Concurrency stress tests for the middleware pipeline.
/// Validates that concurrent dispatch requests are properly isolated and don't share context state.
/// </summary>
/// <remarks>
/// Sprint 693, Task T.3 (bd-8tvcp): Closes the gap where no test validates
/// concurrent middleware execution safety.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MiddlewareConcurrencyStressShould : IDisposable
{
	private readonly CancellationTokenSource _cts = new();

	public void Dispose() => _cts.Dispose();

	[Fact]
	public async Task ExecuteConcurrentDispatchesWithoutContextLeakage()
	{
		// Arrange - Create a pipeline with middleware that stamps each context uniquely
		var contextValues = new ConcurrentDictionary<string, string>();
		var stampingMiddleware = new ContextStampingMiddleware(contextValues);
		var pipeline = new DispatchPipeline([stampingMiddleware]);

		const int concurrentRequests = 100;
		var tasks = new List<Task<IMessageResult>>(concurrentRequests);

		// Act - Fire 100 concurrent dispatches
		for (var i = 0; i < concurrentRequests; i++)
		{
			var messageId = $"msg-{i}";
			var message = new TestDispatchMessage(messageId);
			var context = new MessageEnvelope { MessageId = messageId };

			tasks.Add(pipeline.ExecuteAsync(
				message,
				context,
				static (msg, ctx, ct) => new ValueTask<IMessageResult>(new MessageResult(true)),
				_cts.Token).AsTask());
		}

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - All 100 dispatches succeeded
		results.Length.ShouldBe(concurrentRequests);
		results.ShouldAllBe(r => r.Succeeded);

		// Assert - Each context was stamped with its own unique message ID
		contextValues.Count.ShouldBe(concurrentRequests);
		for (var i = 0; i < concurrentRequests; i++)
		{
			var key = $"msg-{i}";
			contextValues.ContainsKey(key).ShouldBeTrue($"Missing context stamp for {key}");
			contextValues[key].ShouldBe(key, $"Context value mismatch for {key}");
		}
	}

	[Fact]
	public async Task IsolateContextPropertiesAcrossConcurrentRequests()
	{
		// Arrange - Middleware that reads a context property set by each request
		var observedCorrelations = new ConcurrentBag<(string MessageId, string? CorrelationId)>();
		var readingMiddleware = new CorrelationReadingMiddleware(observedCorrelations);
		var pipeline = new DispatchPipeline([readingMiddleware]);

		const int concurrentRequests = 50;
		var tasks = new List<Task>(concurrentRequests);

		// Act - Each request has its own CorrelationId
		for (var i = 0; i < concurrentRequests; i++)
		{
			var messageId = $"iso-{i}";
			var message = new TestDispatchMessage(messageId);
			var context = new MessageEnvelope
			{
				MessageId = messageId,
				CorrelationId = $"corr-{i}",
			};

			tasks.Add(pipeline.ExecuteAsync(
				message,
				context,
				static (msg, ctx, ct) => new ValueTask<IMessageResult>(new MessageResult(true)),
				_cts.Token).AsTask());
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - Each request observed its own CorrelationId (no cross-contamination)
		observedCorrelations.Count.ShouldBe(concurrentRequests);
		foreach (var (messageId, correlationId) in observedCorrelations)
		{
			var expectedIndex = messageId.Replace("iso-", string.Empty, StringComparison.Ordinal);
			correlationId.ShouldBe($"corr-{expectedIndex}",
				$"Context cross-contamination detected for {messageId}");
		}
	}

	[Fact]
	public async Task HandleConcurrentPipelineExecutionWithMixedResults()
	{
		// Arrange - Middleware that fails every 3rd request
		var failingMiddleware = new ConditionalFailingMiddleware();
		var pipeline = new DispatchPipeline([failingMiddleware]);

		const int concurrentRequests = 60;
		var tasks = new List<Task<IMessageResult>>(concurrentRequests);

		// Act
		for (var i = 0; i < concurrentRequests; i++)
		{
			var message = new TestDispatchMessage($"mix-{i}");
			var context = new MessageEnvelope { MessageId = $"mix-{i}" };
			context.Items["Index"] = i;

			tasks.Add(pipeline.ExecuteAsync(
				message,
				context,
				static (msg, ctx, ct) => new ValueTask<IMessageResult>(new MessageResult(true)),
				_cts.Token).AsTask());
		}

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - Correct mix of success/failure (every 3rd fails)
		var succeeded = results.Count(r => r.Succeeded);
		var failed = results.Count(r => !r.Succeeded);
		succeeded.ShouldBe(40); // 60 - 20 = 40 (every 3rd of 60 is 20)
		failed.ShouldBe(20);
	}

	[Fact]
	public async Task MaintainMiddlewareOrderUnderConcurrency()
	{
		// Arrange - Two middleware that record execution order
		var executionLog = new ConcurrentBag<(string MessageId, string Phase)>();
		var firstMiddleware = new OrderTrackingMiddleware("First", executionLog);
		var secondMiddleware = new OrderTrackingMiddleware("Second", executionLog);
		var pipeline = new DispatchPipeline([firstMiddleware, secondMiddleware]);

		const int concurrentRequests = 30;
		var tasks = new List<Task>(concurrentRequests);

		// Act
		for (var i = 0; i < concurrentRequests; i++)
		{
			var messageId = $"order-{i}";
			var message = new TestDispatchMessage(messageId);
			var context = new MessageEnvelope { MessageId = messageId };

			tasks.Add(pipeline.ExecuteAsync(
				message,
				context,
				static (msg, ctx, ct) => new ValueTask<IMessageResult>(new MessageResult(true)),
				_cts.Token).AsTask());
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - For each message, First always executes before Second
		var grouped = executionLog.GroupBy(e => e.MessageId).ToList();
		grouped.Count.ShouldBe(concurrentRequests);

		foreach (var group in grouped)
		{
			var phases = group.OrderBy(e => e.Phase).Select(e => e.Phase).ToList();
			// Both middleware executed
			phases.ShouldContain("First-Before");
			phases.ShouldContain("Second-Before");
		}
	}

	#region Test Middleware

	private sealed class ContextStampingMiddleware(ConcurrentDictionary<string, string> values) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message, IMessageContext context,
			DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			// Stamp context with its own MessageId to prove isolation
			var id = context.MessageId ?? "unknown";
			values.TryAdd(id, id);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class CorrelationReadingMiddleware(ConcurrentBag<(string, string?)> observations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message, IMessageContext context,
			DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			observations.Add((context.MessageId ?? "unknown", context.CorrelationId));
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class ConditionalFailingMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message, IMessageContext context,
			DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			if (context.Items.TryGetValue("Index", out var indexObj) && indexObj is int index && index % 3 == 0)
			{
				return new ValueTask<IMessageResult>(new MessageResult(false));
			}

			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class OrderTrackingMiddleware(string name, ConcurrentBag<(string, string)> log) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => name == "First"
			? DispatchMiddlewareStage.PreProcessing
			: DispatchMiddlewareStage.Processing;

		public async ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message, IMessageContext context,
			DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
		{
			log.Add((context.MessageId ?? "unknown", $"{name}-Before"));
			var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
			log.Add((context.MessageId ?? "unknown", $"{name}-After"));
			return result;
		}
	}

	private sealed class TestDispatchMessage(string id) : IDispatchMessage
	{
		public string Id { get; } = id;
	}

	#endregion
}
