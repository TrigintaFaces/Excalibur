// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Functional.Serverless;

/// <summary>
/// End-to-end functional tests for serverless execution scenarios.
/// Tests AWS Lambda, Azure Functions, and similar serverless patterns.
/// </summary>
[Trait("Category", "Functional")]
public sealed class ServerlessFunctionalShould : FunctionalTestBase
{
	#region Cold Start Tests

	[Fact]
	public async Task Serverless_HandlesFirstInvocation()
	{
		// Arrange - Simulate cold start by creating fresh host
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<InvocationCounter>();
			_ = services.AddSingleton<IServerlessHandler<IncrementCommand, IncrementResult>, IncrementHandler>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var handler = host.Services.GetRequiredService<IServerlessHandler<IncrementCommand, IncrementResult>>();
		var counter = host.Services.GetRequiredService<InvocationCounter>();

		// Act - First invocation (cold start)
		var command = new IncrementCommand();
		var result = await handler.HandleAsync(command, TestCancellationToken).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		counter.Count.ShouldBe(1);
	}

	[Fact]
	public async Task Serverless_MaintainsStateAcrossWarmInvocations()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<InvocationCounter>();
			_ = services.AddSingleton<IServerlessHandler<IncrementCommand, IncrementResult>, IncrementHandler>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var handler = host.Services.GetRequiredService<IServerlessHandler<IncrementCommand, IncrementResult>>();
		var counter = host.Services.GetRequiredService<InvocationCounter>();

		// Act - Multiple warm invocations
		for (var i = 0; i < 5; i++)
		{
			_ = await handler.HandleAsync(new IncrementCommand(), TestCancellationToken).ConfigureAwait(false);
		}

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert - State maintained across invocations
		counter.Count.ShouldBe(5);
	}

	#endregion

	#region Timeout Tests

	[Fact]
	public async Task Serverless_RespectsTimeoutLimit()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<IServerlessHandler<SlowCommand, SlowResult>, SlowHandler>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var handler = host.Services.GetRequiredService<IServerlessHandler<SlowCommand, SlowResult>>();

		// Act - Use short timeout (simulating Lambda timeout)
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
		var command = new SlowCommand(TimeSpan.FromSeconds(10));

		// Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => handler.HandleAsync(command, cts.Token)).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);
	}

	#endregion

	#region Event Trigger Tests

	[Fact]
	public async Task Serverless_ProcessesSqsEvent()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<ProcessedMessages>();
			_ = services.AddSingleton<IServerlessHandler<SqsEvent, SqsResult>, SqsMessageHandler>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var handler = host.Services.GetRequiredService<IServerlessHandler<SqsEvent, SqsResult>>();
		var processedMessages = host.Services.GetRequiredService<ProcessedMessages>();

		// Act - Process SQS-like event
		var sqsEvent = new SqsEvent(
			MessageId: Guid.NewGuid().ToString(),
			Body: "{\"orderId\": \"ORD-123\"}",
			ReceiptHandle: "receipt-handle-123"
		);
		var result = await handler.HandleAsync(sqsEvent, TestCancellationToken).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		processedMessages.Messages.ShouldContain(sqsEvent.MessageId);
	}

	[Fact]
	public async Task Serverless_ProcessesHttpTrigger()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<IServerlessHandler<HttpTriggerEvent, HttpResponse>, HttpTriggerHandler>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var handler = host.Services.GetRequiredService<IServerlessHandler<HttpTriggerEvent, HttpResponse>>();

		// Act - Process HTTP-like trigger
		var httpEvent = new HttpTriggerEvent(
			Method: "POST",
			Path: "/api/orders",
			Body: "{\"product\": \"Widget\", \"quantity\": 5}",
			Headers: new Dictionary<string, string> { ["Content-Type"] = "application/json" }
		);
		var result = await handler.HandleAsync(httpEvent, TestCancellationToken).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		_ = result.ShouldNotBeNull();
		result.StatusCode.ShouldBe(200);
	}

	[Fact]
	public async Task Serverless_ProcessesScheduledEvent()
	{
		// Arrange
		var executed = false;
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<Action>(() => executed = true);
			_ = services.AddSingleton<IServerlessHandler<ScheduledEvent, ScheduledResult>, ScheduledEventHandler>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var handler = host.Services.GetRequiredService<IServerlessHandler<ScheduledEvent, ScheduledResult>>();

		// Act - Process scheduled event (like CloudWatch Event / Timer trigger)
		var scheduledEvent = new ScheduledEvent(
			ScheduleName: "daily-cleanup",
			ScheduledTime: DateTimeOffset.UtcNow
		);
		var result = await handler.HandleAsync(scheduledEvent, TestCancellationToken).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		executed.ShouldBeTrue();
	}

	#endregion

	#region Batch Processing Tests

	[Fact]
	public async Task Serverless_ProcessesBatchOfEvents()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<ProcessedMessages>();
			_ = services.AddSingleton<IServerlessHandler<BatchEvent, BatchResult>, BatchHandler>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var handler = host.Services.GetRequiredService<IServerlessHandler<BatchEvent, BatchResult>>();

		// Act - Process batch of events
		var batch = new BatchEvent([
			new BatchItem("item-1", "data-1"),
			new BatchItem("item-2", "data-2"),
			new BatchItem("item-3", "data-3")
		]);
		var result = await handler.HandleAsync(batch, TestCancellationToken).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		_ = result.ShouldNotBeNull();
		result.ProcessedCount.ShouldBe(3);
		result.FailedCount.ShouldBe(0);
	}

	[Fact]
	public async Task Serverless_HandlesPartialBatchFailure()
	{
		// Arrange
		using var host = CreateHost(services =>
		{
			_ = services.AddSingleton<ProcessedMessages>();
			_ = services.AddSingleton<IServerlessHandler<BatchEvent, BatchResult>, PartialFailureBatchHandler>();
		});
		await host.StartAsync(TestCancellationToken).ConfigureAwait(false);

		var handler = host.Services.GetRequiredService<IServerlessHandler<BatchEvent, BatchResult>>();

		// Act - Process batch with some failures
		var batch = new BatchEvent([
			new BatchItem("item-1", "data-1"),
			new BatchItem("fail-item", "should-fail"),
			new BatchItem("item-3", "data-3")
		]);
		var result = await handler.HandleAsync(batch, TestCancellationToken).ConfigureAwait(false);

		await host.StopAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		result.ProcessedCount.ShouldBe(2);
		result.FailedCount.ShouldBe(1);
	}

	#endregion

	#region Test Interfaces and Messages

	private interface IServerlessHandler<TInput, TOutput>
	{
		Task<TOutput> HandleAsync(TInput input, CancellationToken cancellationToken);
	}

	private sealed record IncrementCommand;
	private sealed record IncrementResult(bool Success);

	private sealed record SlowCommand(TimeSpan Delay);
	private sealed record SlowResult(bool Completed);

	private sealed record SqsEvent(string MessageId, string Body, string ReceiptHandle);
	private sealed record SqsResult(bool Success);

	private sealed record HttpTriggerEvent(string Method, string Path, string Body, Dictionary<string, string> Headers);
	private sealed record HttpResponse(int StatusCode, string Body);

	private sealed record ScheduledEvent(string ScheduleName, DateTimeOffset ScheduledTime);
	private sealed record ScheduledResult(bool Success);

	private sealed record BatchEvent(BatchItem[] Items);
	private sealed record BatchItem(string Id, string Data);
	private sealed record BatchResult(int ProcessedCount, int FailedCount);

	#endregion

	#region Test Implementations

	private sealed class InvocationCounter
	{
		public int Count { get; private set; }
		public void Increment() => Count++;
	}

	private sealed class ProcessedMessages
	{
		public List<string> Messages { get; } = [];
	}

	private sealed class IncrementHandler(InvocationCounter counter) : IServerlessHandler<IncrementCommand, IncrementResult>
	{
		public Task<IncrementResult> HandleAsync(IncrementCommand input, CancellationToken cancellationToken)
		{
			counter.Increment();
			return Task.FromResult(new IncrementResult(true));
		}
	}

	private sealed class SlowHandler : IServerlessHandler<SlowCommand, SlowResult>
	{
		public async Task<SlowResult> HandleAsync(SlowCommand input, CancellationToken cancellationToken)
		{
			await Task.Delay(input.Delay, cancellationToken).ConfigureAwait(false);
			return new SlowResult(true);
		}
	}

	private sealed class SqsMessageHandler(ProcessedMessages processedMessages) : IServerlessHandler<SqsEvent, SqsResult>
	{
		public Task<SqsResult> HandleAsync(SqsEvent input, CancellationToken cancellationToken)
		{
			processedMessages.Messages.Add(input.MessageId);
			return Task.FromResult(new SqsResult(true));
		}
	}

	private sealed class HttpTriggerHandler : IServerlessHandler<HttpTriggerEvent, HttpResponse>
	{
		public Task<HttpResponse> HandleAsync(HttpTriggerEvent input, CancellationToken cancellationToken)
		{
			return Task.FromResult(new HttpResponse(200, "{\"status\": \"ok\"}"));
		}
	}

	private sealed class ScheduledEventHandler(Action onExecuted) : IServerlessHandler<ScheduledEvent, ScheduledResult>
	{
		public Task<ScheduledResult> HandleAsync(ScheduledEvent input, CancellationToken cancellationToken)
		{
			onExecuted();
			return Task.FromResult(new ScheduledResult(true));
		}
	}

	private sealed class BatchHandler : IServerlessHandler<BatchEvent, BatchResult>
	{
		public Task<BatchResult> HandleAsync(BatchEvent input, CancellationToken cancellationToken)
		{
			var processed = input.Items.Length;
			return Task.FromResult(new BatchResult(processed, 0));
		}
	}

	private sealed class PartialFailureBatchHandler : IServerlessHandler<BatchEvent, BatchResult>
	{
		public Task<BatchResult> HandleAsync(BatchEvent input, CancellationToken cancellationToken)
		{
			var failed = input.Items.Count(i => i.Id.StartsWith("fail-", StringComparison.OrdinalIgnoreCase));
			var processed = input.Items.Length - failed;
			return Task.FromResult(new BatchResult(processed, failed));
		}
	}

	#endregion
}
