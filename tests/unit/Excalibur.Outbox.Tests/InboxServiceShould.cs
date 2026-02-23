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

		A.CallTo(() => inbox.RunInboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Invokes(call =>
			{
				capturedDispatcherId = call.GetArgument<string>(0);
			})
			.Returns(Task.FromResult(0));

		var service = new InboxService(inbox, logger);
		using var cts = new CancellationTokenSource();

		// Act - Start the service and let ExecuteAsync run briefly
		await service.StartAsync(cts.Token);
		await Task.Delay(100);
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

		A.CallTo(() => inbox.RunInboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(0));

		var service = new InboxService(inbox, logger);
		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await Task.Delay(100);
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
					await Task.Delay(Timeout.Infinite, ct);
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

		A.CallTo(() => inbox1.RunInboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Invokes(call => dispatcherId1 = call.GetArgument<string>(0))
			.Returns(Task.FromResult(0));

		A.CallTo(() => inbox2.RunInboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Invokes(call => dispatcherId2 = call.GetArgument<string>(0))
			.Returns(Task.FromResult(0));

		var service1 = new InboxService(inbox1, logger);
		var service2 = new InboxService(inbox2, logger);

		using var cts1 = new CancellationTokenSource();
		using var cts2 = new CancellationTokenSource();

		// Act
		await service1.StartAsync(cts1.Token);
		await service2.StartAsync(cts2.Token);
		await Task.Delay(100);

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

		A.CallTo(() => inbox.RunInboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				var ct = call.GetArgument<CancellationToken>(1);
				ct.ThrowIfCancellationRequested();
				await Task.Delay(Timeout.Infinite, ct);
				return 0;
			});

		var service = new InboxService(inbox, logger);
		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await Task.Delay(50);
		await cts.CancelAsync();

		// Assert - Should not throw, should stop gracefully
		await Should.NotThrowAsync(() => service.StopAsync(CancellationToken.None));
	}

	#endregion
}
