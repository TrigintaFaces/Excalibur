// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Tests.Messaging.Outbox;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Unit")]
public sealed class WorkerClaimFixtureShould
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RefuseThePreviousAcquisitionAfterReacquiringTheSameRow(bool completeBeforeReacquiring)
	{
		var store = WorkerClaimFixture.CreateStore();
		var completion = (IClaimScopedOutboxStore)store;
		var row = new OutboundMessage { DispatcherId = "preexisting-stamp" };
		var first = WorkerClaimFixture.Acquire(store, [row]).Single();
		var firstClaim = first.DispatcherId.ShouldNotBeNull();
		firstClaim.ShouldNotBe("preexisting-stamp");
		if (completeBeforeReacquiring)
		{
			(await completion.MarkSentAsync(row.Id, firstClaim, CancellationToken.None))
				.ShouldBe(OutboxCompletionOutcome.Applied);
		}

		var current = WorkerClaimFixture.Acquire(store, [row]).Single();
		var currentClaim = current.DispatcherId.ShouldNotBeNull();
		ReferenceEquals(first, current).ShouldBeFalse();
		ReferenceEquals(row, current).ShouldBeFalse();
		currentClaim.ShouldNotBe(firstClaim);
		first.DispatcherId.ShouldBe(firstClaim);
		row.DispatcherId.ShouldBe("preexisting-stamp");

		(await completion.MarkSentAsync(row.Id, firstClaim, CancellationToken.None))
			.ShouldBe(OutboxCompletionOutcome.ClaimLost);
		(await completion.MarkSentAsync(row.Id, currentClaim, CancellationToken.None))
			.ShouldBe(OutboxCompletionOutcome.Applied);
		(await completion.MarkSentAsync(row.Id, currentClaim, CancellationToken.None))
			.ShouldBe(OutboxCompletionOutcome.ClaimLost);
		WorkerClaimFixture.AssertNoWeakWrites(store);
	}
}
