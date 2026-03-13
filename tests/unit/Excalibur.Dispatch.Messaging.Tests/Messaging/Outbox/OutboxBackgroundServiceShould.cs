// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Outbox.Outbox;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Outbox;

[Trait("Category", "Unit")]
public sealed class OutboxBackgroundServiceShould
{
	private readonly IOutboxPublisher _publisher;
	private readonly ILogger<OutboxBackgroundService> _logger;

	public OutboxBackgroundServiceShould()
	{
		_publisher = A.Fake<IOutboxPublisher>();
		_logger = A.Fake<ILogger<OutboxBackgroundService>>();

		// Default successful results
		_ = A.CallTo(() => _publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.Returns(PublishingResult.Success(0));
		_ = A.CallTo(() => _publisher.PublishScheduledMessagesAsync(A<CancellationToken>._))
			.Returns(PublishingResult.Success(0));
		_ = A.CallTo(() => _publisher.RetryFailedMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(PublishingResult.Success(0));
	}

	[Fact]
	public async Task ProcessPendingMessagesOnEachCycle()
	{
		// Arrange
		var callCount = 0;
		var pendingObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		_ = A.CallTo(() => _publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.Invokes(() =>
			{
				Interlocked.Increment(ref callCount);
				_ = pendingObserved.TrySetResult(true);
			})
			.Returns(PublishingResult.Success(0));

		var options = CreateOptions(pollingInterval: TimeSpan.FromMilliseconds(100));
		var service = new OutboxBackgroundService(_publisher, options, _logger);
		var cts = new CancellationTokenSource();

		// Act
		var task = service.StartAsync(cts.Token);

		// Poll until the publisher has been called at least once (generous timeout for CI load)
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			pendingObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30))).ConfigureAwait(false);

		cts.Cancel();
		await task;

		// Assert
		callCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task ProcessScheduledMessagesWhenEnabled()
	{
		// Arrange
		var callCount = 0;
		var scheduledObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		_ = A.CallTo(() => _publisher.PublishScheduledMessagesAsync(A<CancellationToken>._))
			.Invokes(() =>
			{
				Interlocked.Increment(ref callCount);
				_ = scheduledObserved.TrySetResult(true);
			})
			.Returns(PublishingResult.Success(0));

		var options = CreateOptions(
			pollingInterval: TimeSpan.FromMilliseconds(100),
			processScheduled: true);
		var service = new OutboxBackgroundService(_publisher, options, _logger);
		var cts = new CancellationTokenSource();

		// Act
		var task = service.StartAsync(cts.Token);

		// Poll until the scheduled publisher has been called at least once (generous timeout for CI load)
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			scheduledObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30))).ConfigureAwait(false);

		cts.Cancel();
		await task;

		// Assert
		callCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task SkipScheduledMessagesWhenDisabled()
	{
		// Arrange
		var pendingObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var scheduledObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		_ = A.CallTo(() => _publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.Invokes(() => _ = pendingObserved.TrySetResult(true))
			.Returns(PublishingResult.Success(0));
		_ = A.CallTo(() => _publisher.PublishScheduledMessagesAsync(A<CancellationToken>._))
			.Invokes(() => _ = scheduledObserved.TrySetResult(true))
			.Returns(PublishingResult.Success(0));
		var options = CreateOptions(
			pollingInterval: TimeSpan.FromMilliseconds(100),
			processScheduled: false);
		var service = new OutboxBackgroundService(_publisher, options, _logger);
		var cts = new CancellationTokenSource();

		// Act
		var task = service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			pendingObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
		cts.Cancel();
		await task;

		// Assert
		scheduledObserved.Task.IsCompleted.ShouldBeFalse();
		A.CallTo(() => _publisher.PublishScheduledMessagesAsync(A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task RetryFailedMessagesWhenEnabled()
	{
		// Arrange
		var callCount = 0;
		var retryObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		_ = A.CallTo(() => _publisher.RetryFailedMessagesAsync(5, A<CancellationToken>._))
			.Invokes(() =>
			{
				Interlocked.Increment(ref callCount);
				_ = retryObserved.TrySetResult(true);
			})
			.Returns(PublishingResult.Success(0));

		var options = CreateOptions(
			pollingInterval: TimeSpan.FromMilliseconds(100),
			retryFailed: true,
			maxRetries: 5);
		var service = new OutboxBackgroundService(_publisher, options, _logger);
		var cts = new CancellationTokenSource();

		// Act
		var task = service.StartAsync(cts.Token);

		// Poll until the retry method has been called at least once (generous timeout for cross-process CPU starvation under full-suite VS Test Explorer load)
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			retryObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(120))).ConfigureAwait(false);

		cts.Cancel();
		await task;

		// Assert
		callCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task SkipRetryWhenDisabled()
	{
		// Arrange
		var pendingObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var retryObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		_ = A.CallTo(() => _publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.Invokes(() => _ = pendingObserved.TrySetResult(true))
			.Returns(PublishingResult.Success(0));
		_ = A.CallTo(() => _publisher.RetryFailedMessagesAsync(A<int>._, A<CancellationToken>._))
			.Invokes(() => _ = retryObserved.TrySetResult(true))
			.Returns(PublishingResult.Success(0));
		var options = CreateOptions(
			pollingInterval: TimeSpan.FromMilliseconds(100),
			retryFailed: false);
		var service = new OutboxBackgroundService(_publisher, options, _logger);
		var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			pendingObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(60))).ConfigureAwait(false);
		cts.Cancel();
		await service.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		retryObserved.Task.IsCompleted.ShouldBeFalse();
		A.CallTo(() => _publisher.RetryFailedMessagesAsync(A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task NotProcessWhenDisabled()
	{
		// Arrange
		var pendingObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		_ = A.CallTo(() => _publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.Invokes(() => _ = pendingObserved.TrySetResult(true))
			.Returns(PublishingResult.Success(0));
		var options = CreateOptions(enabled: false);
		var service = new OutboxBackgroundService(_publisher, options, _logger);
		var cts = new CancellationTokenSource();

		// Act
		var task = service.StartAsync(cts.Token);
		var unexpectedProcessingObserved = false;
		try
		{
			await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
				pendingObserved.Task,
				global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromMilliseconds(250))).ConfigureAwait(false);
			unexpectedProcessingObserved = true;
		}
		catch (TimeoutException)
		{
			unexpectedProcessingObserved = false;
		}
		cts.Cancel();
		await task;

		// Assert
		unexpectedProcessingObserved.ShouldBeFalse();
		A.CallTo(() => _publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ContinueAfterPublisherError()
	{
		// Arrange
		var callCount = 0;
		var firstFailureObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var recoveryObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		_ = A.CallTo(() => _publisher.PublishPendingMessagesAsync(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var current = Interlocked.Increment(ref callCount);
				if (current == 1)
				{
					_ = firstFailureObserved.TrySetResult();
					throw new InvalidOperationException("Test error");
				}
				_ = recoveryObserved.TrySetResult(true);
				return Task.FromResult(PublishingResult.Success(1));
			});

		var options = CreateOptions(pollingInterval: TimeSpan.FromMilliseconds(100));
		var service = new OutboxBackgroundService(_publisher, options, _logger);
		var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			firstFailureObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(60))).ConfigureAwait(false);

		// Poll until at least 2 calls (one error + one recovery), generous timeout for full-suite load
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			recoveryObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(60))).ConfigureAwait(false);

		cts.Cancel();
		await service.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert - service recovered from the first error and continued processing
		// Under extreme full-suite parallel load, the background service timer may be starved,
		// so we verify at least the first call happened (error was thrown but didn't crash the service)
		callCount.ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public async Task StopGracefullyOnCancellation()
	{
		// Arrange
		var options = CreateOptions(pollingInterval: TimeSpan.FromSeconds(10));
		var service = new OutboxBackgroundService(_publisher, options, _logger);
		var cts = new CancellationTokenSource();

		// Act
		var task = service.StartAsync(cts.Token);
		cts.Cancel();

		// Assert - should complete within reasonable time
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
	}

	[Fact]
	public void ThrowWhenPublisherIsNull()
	{
		// Arrange
		var options = CreateOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new OutboxBackgroundService(null!, options, _logger));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new OutboxBackgroundService(_publisher, null!, _logger));
	}

	private static IOptions<OutboxProcessingOptions> CreateOptions(
		TimeSpan? pollingInterval = null,
		int maxRetries = 3,
		bool processScheduled = true,
		bool retryFailed = true,
		bool enabled = true)
	{
		var options = new OutboxProcessingOptions
		{
			PollingInterval = pollingInterval ?? TimeSpan.FromSeconds(5),
			MaxRetries = maxRetries,
			ProcessScheduledMessages = processScheduled,
			RetryFailedMessages = retryFailed,
			Enabled = enabled
		};

		return Microsoft.Extensions.Options.Options.Create(options);
	}
}

