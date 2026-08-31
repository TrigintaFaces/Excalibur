// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;
using Excalibur.Dispatch.Transport.GooglePubSub.Internal;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub;

/// <summary>
/// Binds <see cref="PubSubTransportReceiver"/>'s configured request timeout to the pull it is meant to bound.
/// </summary>
/// <remarks>
/// <para>
/// The receiver accepts a request timeout and must apply it to the pull. A pull that never returns would
/// otherwise stall the receive loop for as long as the caller's token stays live, which for a long-running
/// consumer is indefinitely — the configured timeout being silently dropped is indistinguishable, from the
/// caller's side, from a subscription with no traffic.
/// </para>
/// <para>
/// The arms are differential, so the timeout cannot be satisfied by hardcoding one: a configured timeout
/// MUST end the pull, an unconfigured timeout MUST leave it governed by the caller's token alone (the
/// documented default), and a configured timeout MUST still observe caller cancellation — proving the
/// timeout is linked to the caller's token rather than replacing it. Each arm waits on a generous bound
/// well above the timeout under test, so the assertions do not race a loaded machine.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class PubSubReceiverRequestTimeoutShould
{
	private const string Subscription = "projects/test/subscriptions/test-sub";

	private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(200);
	private static readonly TimeSpan GenerousBound = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan StillRunningWindow = TimeSpan.FromSeconds(1);

	[Fact]
	public async Task BoundThePull_ByTheConfiguredRequestTimeout()
	{
		var receiver = CreateReceiver(HangingClient(), ShortTimeout);

		// The caller's token never cancels, so ONLY the configured timeout can end this pull.
		var receive = receiver.ReceiveAsync(10, CancellationToken.None);

		var finished = await Task.WhenAny(receive, Task.Delay(GenerousBound)).ConfigureAwait(false);

		finished.ShouldBeSameAs(receive, "the configured request timeout did not bound the pull");
		_ = await Should.ThrowAsync<OperationCanceledException>(() => receive).ConfigureAwait(false);
	}

	[Fact]
	public async Task LeaveThePullUnbounded_WhenNoRequestTimeoutIsConfigured()
	{
		using var callerCts = new CancellationTokenSource();
		var receiver = CreateReceiver(HangingClient(), requestTimeout: default);

		var receive = receiver.ReceiveAsync(10, callerCts.Token);

		// Safety: the default must not acquire a timeout of its own — the caller's token still governs.
		var finished = await Task.WhenAny(receive, Task.Delay(StillRunningWindow)).ConfigureAwait(false);
		finished.ShouldNotBeSameAs(receive, "an unconfigured request timeout must leave the pull unbounded");

		// And the caller's token is what ends it.
		await callerCts.CancelAsync().ConfigureAwait(false);
		_ = await Should.ThrowAsync<OperationCanceledException>(() => receive).ConfigureAwait(false);
	}

	[Fact]
	public async Task StillHonorCallerCancellation_WhenARequestTimeoutIsConfigured()
	{
		using var callerCts = new CancellationTokenSource();

		// A timeout far beyond the test's bound, so only caller cancellation can end this pull.
		var receiver = CreateReceiver(HangingClient(), TimeSpan.FromMinutes(5));

		var receive = receiver.ReceiveAsync(10, callerCts.Token);
		await callerCts.CancelAsync().ConfigureAwait(false);

		var finished = await Task.WhenAny(receive, Task.Delay(GenerousBound)).ConfigureAwait(false);

		finished.ShouldBeSameAs(receive, "caller cancellation must still reach the pull when a timeout is configured");
		_ = await Should.ThrowAsync<OperationCanceledException>(() => receive).ConfigureAwait(false);
	}

	/// <summary>
	/// A client whose pull completes only when the token the receiver handed it is cancelled — so the
	/// token the receiver chose to pass is the only thing that can end the call.
	/// </summary>
	private static ISubscriberApiClientSeam HangingClient()
	{
		var client = A.Fake<ISubscriberApiClientSeam>();
		A.CallTo(() => client.PullAsync(A<PullRequest>._, A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				var token = call.Arguments.Get<CancellationToken>(1);
				var pull = new TaskCompletionSource<PullResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
				_ = token.Register(() => pull.TrySetCanceled(token));
				return pull.Task;
			});

		return client;
	}

	private static PubSubTransportReceiver CreateReceiver(ISubscriberApiClientSeam client, TimeSpan requestTimeout) =>
		new(client, Subscription, NullLogger<PubSubTransportReceiver>.Instance, requestTimeout: requestTimeout);
}
