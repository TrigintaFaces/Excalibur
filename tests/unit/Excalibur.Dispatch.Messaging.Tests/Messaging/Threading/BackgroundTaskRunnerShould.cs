// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Threading;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Threading;

/// <summary>
///     Tests for the <see cref="BackgroundTaskRunner" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class BackgroundTaskRunnerShould
{
	[Fact]
	public async Task RunTaskInBackground()
	{
		var executed = new TaskCompletionSource<bool>();

		BackgroundTaskRunner.RunDetachedInBackground(
			async ct =>
			{
				await Task.Yield();
				executed.SetResult(true);
			},
			CancellationToken.None);

		var result = await executed.Task.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeErrorHandlerOnException()
	{
		var errorReceived = new TaskCompletionSource<Exception>();

		BackgroundTaskRunner.RunDetachedInBackground(
			ct => throw new InvalidOperationException("test error"),
			CancellationToken.None,
			onError: ex =>
			{
				errorReceived.SetResult(ex);
				return Task.CompletedTask;
			});

		var exception = await errorReceived.Task.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
		exception.ShouldBeOfType<InvalidOperationException>();
		exception.Message.ShouldBe("test error");
	}

	[Fact]
	public async Task LogExceptionWhenNoErrorHandler()
	{
		var logger = A.Fake<ILogger>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);
		var logObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(logger)
			.Where(call => call.Method.Name == nameof(ILogger.Log))
			.Invokes(_ => logObserved.TrySetResult(true));

		var completed = new TaskCompletionSource<bool>();

		BackgroundTaskRunner.RunDetachedInBackground(
			ct =>
			{
				completed.SetResult(true);
				throw new InvalidOperationException("test error");
			},
			CancellationToken.None,
			logger: logger);

		await completed.Task.WaitAsync(TimeSpan.FromSeconds(120)).ConfigureAwait(false);
		await logObserved.Task.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
	}

	[Fact]
	public async Task PassCancellationTokenToTask()
	{
		using var cts = new CancellationTokenSource();
		var receivedToken = new TaskCompletionSource<CancellationToken>();

		BackgroundTaskRunner.RunDetachedInBackground(
			ct =>
			{
				receivedToken.SetResult(ct);
				return Task.CompletedTask;
			},
			cts.Token);

		var token = await receivedToken.Task.WaitAsync(TimeSpan.FromSeconds(120)).ConfigureAwait(false);
		token.ShouldBe(cts.Token);
	}

	[Fact]
	public void AcceptNullErrorHandler()
	{
		Should.NotThrow(() =>
			BackgroundTaskRunner.RunDetachedInBackground(
				ct => Task.CompletedTask,
				CancellationToken.None,
				onError: null));
	}

	[Fact]
	public void AcceptNullLogger()
	{
		Should.NotThrow(() =>
			BackgroundTaskRunner.RunDetachedInBackground(
				ct => Task.CompletedTask,
				CancellationToken.None,
				logger: null));
	}
}
