// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;
using Excalibur.Dispatch.Transport.GooglePubSub.Internal;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Wiring;

/// <summary>
/// Locks the configured pull size onto the Pub/Sub pull request. The receiver clamps the caller's
/// request to the configured ceiling, so a ceiling that never reached the receiver would cap every
/// pull at the built-in default no matter what the consumer configured.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class PubSubPullSizeWiringShould
{
	[Fact]
	public async Task RaiseThePullCeilingToTheConfiguredMaxPullMessages()
	{
		var captured = await PullWithAsync(maxMessages: 250, requested: 200).ConfigureAwait(true);

		// 200 is above the built-in ceiling of 10: it only survives if the configured 250 arrived.
		captured.MaxMessages.ShouldBe(200);
	}

	[Fact]
	public async Task StillClampACallerRequestAboveTheConfiguredCeiling()
	{
		var captured = await PullWithAsync(maxMessages: 25, requested: 200).ConfigureAwait(true);

		captured.MaxMessages.ShouldBe(25);
	}

	private static async Task<PullRequest> PullWithAsync(int maxMessages, int requested)
	{
		PullRequest? captured = null;
		var client = A.Fake<ISubscriberApiClientSeam>();
		_ = A.CallTo(() => client.PullAsync(A<PullRequest>._, A<CancellationToken>._))
			.Invokes((PullRequest r, CancellationToken _) => captured = r)
			.Returns(Task.FromResult(new PullResponse()));

		var receiver = new PubSubTransportReceiver(
			client,
			"projects/p/subscriptions/s",
			NullLogger<PubSubTransportReceiver>.Instance,
			maxMessages: maxMessages);

		_ = await receiver.ReceiveAsync(requested, CancellationToken.None).ConfigureAwait(true);

		captured.ShouldNotBeNull();
		return captured!;
	}
}
