// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Transport.IbmMq;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.IbmMq;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class IbmMqReceiveBackpressureShould
{
	[Fact]
	public void DefaultMaxOutstandingUnitsOfWorkToTheConnectionCeiling()
	{
		var options = new IbmMqReceiveTuningOptions();

		options.MaxOutstandingUnitsOfWork.ShouldBe(IbmMqReceiveTuningOptions.MaxBatchSizeCeiling);
		options.MaxOutstandingUnitsOfWork.ShouldBe(256);
	}

	[Fact]
	public async Task ReturnEmptyAndOpenNoConnectionWhenOutstandingCapIsSaturated()
	{
		// Saturated cap (0 remaining capacity from an empty outstanding set) must short-circuit BEFORE
		// opening a queue-manager connection — that is the back-pressure that prevents connection-pool
		// exhaustion under slow acknowledgement. If the cap check is removed, the receiver would call
		// CreateQueueManager and this lock goes RED.
		var provider = A.Fake<IIbmMqConnectionProvider>();
		var options = new IbmMqReceiveTuningOptions { MaxOutstandingUnitsOfWork = 0 };
		var receiver = new IbmMqTransportReceiver(
			provider,
			"DEV.QUEUE.1",
			options,
			NullLogger<IbmMqTransportReceiver>.Instance);

		var result = await receiver.ReceiveAsync(10, CancellationToken.None);

		result.ShouldBeEmpty();
		A.CallTo(() => provider.CreateQueueManager()).MustNotHaveHappened();
	}
}
