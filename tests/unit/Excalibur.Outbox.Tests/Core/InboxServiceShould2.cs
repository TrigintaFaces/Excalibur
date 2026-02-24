// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery;
using Excalibur.Outbox.Health;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Outbox.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InboxServiceShould2
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

	[Fact]
	public void ThrowWhenInboxIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new InboxService(null!, NullLogger<InboxService>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		var inbox = A.Fake<IInbox>();
		Should.Throw<ArgumentNullException>(() =>
			new InboxService(inbox, null!));
	}

	[Fact]
	public async Task StartAndStopGracefully()
	{
		// Arrange
		var inbox = A.Fake<IInbox>();
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

		var service = new InboxService(
			inbox,
			NullLogger<InboxService>.Instance);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await dispatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => inbox.DisposeAsync())
			.MustHaveHappened();
	}

	[Fact]
	public async Task UseHealthStateWhenProvided()
	{
		// Arrange
		var inbox = A.Fake<IInbox>();
		var healthState = new BackgroundServiceHealthState();
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

		var service = new InboxService(
			inbox,
			NullLogger<InboxService>.Instance,
			healthState);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await dispatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - no exception
	}

	[Fact]
	public async Task AcceptCustomDrainTimeout()
	{
		// Arrange
		var inbox = A.Fake<IInbox>();
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

		var service = new InboxService(
			inbox,
			NullLogger<InboxService>.Instance,
			drainTimeoutSeconds: 5);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await dispatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - no exception
	}
}
