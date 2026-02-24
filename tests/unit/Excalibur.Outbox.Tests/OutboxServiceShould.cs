// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery;

using FakeItEasy;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="OutboxService"/>.
/// Tests the background service wrapper for outbox dispatching.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Priority", "0")]
public sealed class OutboxServiceShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Constructor_CreatesService_WithValidOutbox()
	{
		// Arrange
		var outbox = A.Fake<IOutboxDispatcher>();

		// Act
		var service = new OutboxService(outbox);

		// Assert
		_ = service.ShouldNotBeNull();
	}

	#endregion

	#region StopAsync Tests

	[Fact]
	public async Task StopAsync_DisposesOutbox()
	{
		// Arrange
		var outbox = A.Fake<IOutboxDispatcher>();
		var service = new OutboxService(outbox);

		// Act
		await service.StopAsync(CancellationToken.None);

		// Assert
		_ = A.CallTo(() => outbox.DisposeAsync()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task StopAsync_HandlesAlreadyDisposedOutbox()
	{
		// Arrange
		var outbox = A.Fake<IOutboxDispatcher>();
		_ = A.CallTo(() => outbox.DisposeAsync())
			.ThrowsAsync(new ObjectDisposedException(nameof(outbox)));

		var service = new OutboxService(outbox);

		// Act & Assert - Should propagate the exception
		_ = await Should.ThrowAsync<ObjectDisposedException>(
			() => service.StopAsync(CancellationToken.None));
	}

	#endregion

	#region ExecuteAsync Tests

	[Fact]
	public async Task StartAsync_CallsRunOutboxDispatchAsync_WithGeneratedDispatcherId()
	{
		// Arrange
		var outbox = A.Fake<IOutboxDispatcher>();
		string? capturedDispatcherId = null;
		var dispatchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		_ = A.CallTo(() => outbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
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
					await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(Timeout.Infinite, ct);
				}
				catch (OperationCanceledException)
				{
					// Expected
				}
				return 0;
			});

		var service = new OutboxService(outbox);

		using var cts = new CancellationTokenSource();

		// Act - Start the service and let ExecuteAsync run briefly
		await service.StartAsync(cts.Token);
		await dispatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - DispatcherId should be a valid non-empty string
		capturedDispatcherId.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task StartAsync_PassesCancellationToken_ToRunOutboxDispatchAsync()
	{
		// Arrange
		var outbox = A.Fake<IOutboxDispatcher>();
		CancellationToken capturedToken = default;
		var dispatchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		_ = A.CallTo(() => outbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Invokes(call =>
			{
				capturedToken = call.GetArgument<CancellationToken>(1);
				_ = dispatchStarted.TrySetResult();
			})
			.ReturnsLazily(async call =>
			{
				var ct = call.GetArgument<CancellationToken>(1);
				try
				{
					await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(Timeout.Infinite, ct);
				}
				catch (OperationCanceledException)
				{
					// Expected
				}
				return 0;
			});

		var service = new OutboxService(outbox);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await dispatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - Token should have been passed (it may already be cancelled)
		_ = A.CallTo(() => outbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.MustHaveHappened();
		capturedToken.CanBeCanceled.ShouldBeTrue();
	}

	#endregion
}

