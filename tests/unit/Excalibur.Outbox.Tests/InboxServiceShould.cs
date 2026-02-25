// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery;
using Excalibur.Outbox.Health;

using FakeItEasy;

using Microsoft.Extensions.Logging;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="InboxService"/>.
/// Tests the background service wrapper for inbox dispatching.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Priority", "0")]
public sealed class InboxServiceShould : UnitTestBase
{
	private static async Task WaitUntilCancelledAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var registration = cancellationToken.Register(static state =>
		{
			var tcs = (TaskCompletionSource)state!;
			tcs.TrySetResult();
		}, completion);
		await completion.Task.ConfigureAwait(false);
		await registration.DisposeAsync().ConfigureAwait(false);
		throw new OperationCanceledException(cancellationToken);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_CreatesService_WithValidDependencies()
	{
		// Arrange
		var inbox = A.Fake<IInbox>();
		var logger = A.Fake<ILogger<InboxService>>();

		// Act
		var service = new InboxService(inbox, logger);

		// Assert
		service.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenInboxIsNull()
	{
		// Arrange
		var logger = A.Fake<ILogger<InboxService>>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new InboxService(null!, logger));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var inbox = A.Fake<IInbox>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new InboxService(inbox, null!));
	}

	[Fact]
	public void Constructor_AcceptsOptionalHealthState()
	{
		// Arrange
		var inbox = A.Fake<IInbox>();
		var logger = A.Fake<ILogger<InboxService>>();
		var healthState = new BackgroundServiceHealthState();

		// Act
		var service = new InboxService(inbox, logger, healthState);

		// Assert
		service.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_AcceptsCustomDrainTimeout()
	{
		// Arrange
		var inbox = A.Fake<IInbox>();
		var logger = A.Fake<ILogger<InboxService>>();

		// Act
		var service = new InboxService(inbox, logger, drainTimeoutSeconds: 60);

		// Assert
		service.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_AcceptsAllParameters()
	{
		// Arrange
		var inbox = A.Fake<IInbox>();
		var logger = A.Fake<ILogger<InboxService>>();
		var healthState = new BackgroundServiceHealthState();

		// Act
		var service = new InboxService(inbox, logger, healthState, drainTimeoutSeconds: 45);

		// Assert
		service.ShouldNotBeNull();
	}

	#endregion

	#region StopAsync Tests

	[Fact]
	public async Task StopAsync_DisposesInbox()
	{
		// Arrange
		var inbox = A.Fake<IInbox>();
		var logger = A.Fake<ILogger<InboxService>>();
		var service = new InboxService(inbox, logger);

		// Act
		await service.StopAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => inbox.DisposeAsync()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task StopAsync_MarksHealthStateStopped_WhenHealthStateProvided()
	{
		// Arrange
		var inbox = A.Fake<IInbox>();
		var logger = A.Fake<ILogger<InboxService>>();
		var healthState = new BackgroundServiceHealthState();
		healthState.MarkStarted();

		var service = new InboxService(inbox, logger, healthState);

		// Act
		await service.StopAsync(CancellationToken.None);

		// Assert
		healthState.IsRunning.ShouldBeFalse();
	}

	[Fact]
	public async Task StopAsync_HandlesNullHealthState()
	{
		// Arrange
		var inbox = A.Fake<IInbox>();
		var logger = A.Fake<ILogger<InboxService>>();
		var service = new InboxService(inbox, logger, healthState: null);

		// Act & Assert - Should not throw
		await Should.NotThrowAsync(() => service.StopAsync(CancellationToken.None));
	}

	[Fact]
	public async Task StopAsync_PropagatesException_WhenInboxDisposeFails()
	{
		// Arrange
		var inbox = A.Fake<IInbox>();
		var logger = A.Fake<ILogger<InboxService>>();
		A.CallTo(() => inbox.DisposeAsync())
			.ThrowsAsync(new InvalidOperationException("Dispose failed"));

		var service = new InboxService(inbox, logger);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => service.StopAsync(CancellationToken.None));
	}

	[Fact]
	public async Task StopAsync_PropagatesObjectDisposedException_WhenInboxAlreadyDisposed()
	{
		// Arrange
		var inbox = A.Fake<IInbox>();
		var logger = A.Fake<ILogger<InboxService>>();
		A.CallTo(() => inbox.DisposeAsync())
			.ThrowsAsync(new ObjectDisposedException(nameof(inbox)));

		var service = new InboxService(inbox, logger);

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(
			() => service.StopAsync(CancellationToken.None));
	}

	#endregion

	#region ExecuteAsync Tests

	[Fact]
	public async Task StartAsync_CallsRunInboxDispatchAsync_WithGeneratedDispatcherId()
	{
		// Arrange
		var inbox = A.Fake<IInbox>();
		var logger = A.Fake<ILogger<InboxService>>();
		string? capturedDispatcherId = null;
		var dispatchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => inbox.RunInboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Invokes(call =>
			{
				capturedDispatcherId = call.GetArgument<string>(0);
				_ = dispatchStarted.TrySetResult();
			})
			.ReturnsLazily(async call =>
			{
				var ct = call.GetArgument<CancellationToken>(1);
				try
				{
					await WaitUntilCancelledAsync(ct);
				}
				catch (OperationCanceledException)
				{
					// Expected
				}
				return 0;
			});

		var service = new InboxService(inbox, logger);
		using var cts = new CancellationTokenSource();

		// Act - Start the service and let ExecuteAsync run briefly
		await service.StartAsync(cts.Token);
		var dispatchObserved = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => dispatchStarted.Task.IsCompleted,
			TimeSpan.FromSeconds(10),
			TimeSpan.FromMilliseconds(20));
		dispatchObserved.ShouldBeTrue("inbox dispatch should start");
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - DispatcherId should be a valid non-empty string
		capturedDispatcherId.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task StartAsync_PassesCancellationToken_ToRunInboxDispatchAsync()
	{
		// Arrange
		var inbox = A.Fake<IInbox>();
		var logger = A.Fake<ILogger<InboxService>>();
		var dispatchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => inbox.RunInboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Invokes(() => _ = dispatchStarted.TrySetResult())
			.ReturnsLazily(async call =>
			{
				var ct = call.GetArgument<CancellationToken>(1);
				try
				{
					await WaitUntilCancelledAsync(ct);
				}
				catch (OperationCanceledException)
				{
					// Expected
				}
				return 0;
			});

		var service = new InboxService(inbox, logger);
		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		var dispatchObserved = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => dispatchStarted.Task.IsCompleted,
			TimeSpan.FromSeconds(10),
			TimeSpan.FromMilliseconds(20));
		dispatchObserved.ShouldBeTrue("inbox dispatch should start");
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => inbox.RunInboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task StartAsync_MarksHealthStateStarted_WhenHealthStateProvided()
	{
		// Arrange
		var inbox = A.Fake<IInbox>();
		var logger = A.Fake<ILogger<InboxService>>();
		var healthState = new BackgroundServiceHealthState();

		// Delay return to allow health state check
		A.CallTo(() => inbox.RunInboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				var ct = call.GetArgument<CancellationToken>(1);
				try
				{
					await WaitUntilCancelledAsync(ct);
				}
				catch (OperationCanceledException)
				{
					// Expected
				}
				return 0;
			});

		var service = new InboxService(inbox, logger, healthState);
		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await WaitUntilAsync(
			() => healthState.IsRunning,
			TimeSpan.FromSeconds(2));

		// Assert - Health state should be running
		healthState.IsRunning.ShouldBeTrue();

		// Cleanup
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);
	}

	#endregion

	#region DispatcherId Generation Tests

	[Fact]
	public async Task EachServiceInstance_HasUniqueDispatcherId()
	{
		// Arrange
		var inbox1 = A.Fake<IInbox>();
		var inbox2 = A.Fake<IInbox>();
		var logger = A.Fake<ILogger<InboxService>>();

		string? dispatcherId1 = null;
		string? dispatcherId2 = null;
		var service1DispatchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var service2DispatchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => inbox1.RunInboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Invokes(call =>
			{
				dispatcherId1 = call.GetArgument<string>(0);
				_ = service1DispatchStarted.TrySetResult();
			})
			.ReturnsLazily(async call =>
			{
				var ct = call.GetArgument<CancellationToken>(1);
				try
				{
					await WaitUntilCancelledAsync(ct);
				}
				catch (OperationCanceledException)
				{
					// Expected
				}
				return 0;
			});

		A.CallTo(() => inbox2.RunInboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Invokes(call =>
			{
				dispatcherId2 = call.GetArgument<string>(0);
				_ = service2DispatchStarted.TrySetResult();
			})
			.ReturnsLazily(async call =>
			{
				var ct = call.GetArgument<CancellationToken>(1);
				try
				{
					await WaitUntilCancelledAsync(ct);
				}
				catch (OperationCanceledException)
				{
					// Expected
				}
				return 0;
			});

		var service1 = new InboxService(inbox1, logger);
		var service2 = new InboxService(inbox2, logger);

		using var cts1 = new CancellationTokenSource();
		using var cts2 = new CancellationTokenSource();

		// Act
		await service1.StartAsync(cts1.Token);
		await service2.StartAsync(cts2.Token);
		var service1DispatchObserved = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => service1DispatchStarted.Task.IsCompleted,
			TimeSpan.FromSeconds(10),
			TimeSpan.FromMilliseconds(20));
		service1DispatchObserved.ShouldBeTrue("first service should start dispatching");
		var service2DispatchObserved = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => service2DispatchStarted.Task.IsCompleted,
			TimeSpan.FromSeconds(10),
			TimeSpan.FromMilliseconds(20));
		service2DispatchObserved.ShouldBeTrue("second service should start dispatching");

		await cts1.CancelAsync();
		await cts2.CancelAsync();
		await service1.StopAsync(CancellationToken.None);
		await service2.StopAsync(CancellationToken.None);

		// Assert - Each service should have a unique dispatcher ID
		dispatcherId1.ShouldNotBeNullOrWhiteSpace();
		dispatcherId2.ShouldNotBeNullOrWhiteSpace();
		dispatcherId1.ShouldNotBe(dispatcherId2);
	}

	#endregion

	#region Cancellation Handling Tests

	[Fact]
	public async Task ExecuteAsync_HandlesOperationCanceledException_Gracefully()
	{
		// Arrange
		var inbox = A.Fake<IInbox>();
		var logger = A.Fake<ILogger<InboxService>>();

		var dispatchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => inbox.RunInboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				var ct = call.GetArgument<CancellationToken>(1);
				_ = dispatchStarted.TrySetResult();
				ct.ThrowIfCancellationRequested();
				await WaitUntilCancelledAsync(ct);
				return 0;
			});

		var service = new InboxService(inbox, logger);
		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		var dispatchObserved = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => dispatchStarted.Task.IsCompleted,
			TimeSpan.FromSeconds(10),
			TimeSpan.FromMilliseconds(20));
		dispatchObserved.ShouldBeTrue("dispatch should start before cancellation");
		await cts.CancelAsync();

		// Assert - Should not throw, should stop gracefully
		await Should.NotThrowAsync(() => service.StopAsync(CancellationToken.None));
	}

	#endregion
}

