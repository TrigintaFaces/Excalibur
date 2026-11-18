// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Outbox.InMemory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.InMemory.Tests.InMemory;

/// <summary>
/// Verifies reservation-ownership safety and current-owner progress against real public claim operations.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Outbox")]
public sealed class InMemoryOutboxMarkFailedR2GuardShould
{
	private static InMemoryOutboxStore CreateStore(int floorSeconds) => new(
		Options.Create(new InMemoryOutboxOptions { FailureBackoffFloorSeconds = floorSeconds }),
		NullLogger<InMemoryOutboxStore>.Instance);

	private static OutboundMessage NewMessage() => new("TestMessageType", [1, 2, 3], "test-destination");

	[Fact]
	public async Task NonOwningMarkFailed_DoesNotStealTheLease_NorRecordTheFailure()
	{
		using var store = CreateStore(floorSeconds: 120);
		var msg = NewMessage();
		await store.StageMessageAsync(msg, CancellationToken.None);
		var owner = (await store.GetUnsentMessagesAsync(1, 7, CancellationToken.None)).Single();
		var nonOwner = owner.DispatcherId + ":non-owner";
		(await store.MarkFailedAsync(msg.Id, "stolen-lease", 1, null,
			new OutboxWriteAuthority(7, nonOwner), CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.ClaimLost);

		// A second acquisition cannot enter, and the original evidence still completes: the lease survived.
		(await store.GetUnsentMessagesAsync(1, 7, CancellationToken.None)).ShouldBeEmpty();
		(await store.GetAllTenantsFailedMessagesAsync(100, null, 10, CancellationToken.None))
			.ShouldNotContain(m => m.Id == msg.Id, "a non-owner must not record a failure");
		(await store.MarkFailedAsync(msg.Id, "owner-fail", 1, null,
			new OutboxWriteAuthority(7, owner.DispatcherId!), CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.Applied);
	}

	[Fact]
	public async Task OwningMarkFailed_IsRecorded()
	{
		using var store = CreateStore(floorSeconds: 120);
		var msg = NewMessage();
		await store.StageMessageAsync(msg, CancellationToken.None);
		var owner = (await store.GetUnsentMessagesAsync(1, 7, CancellationToken.None)).Single();
		(await store.MarkFailedAsync(msg.Id, "owner-fail", 1, null,
			new OutboxWriteAuthority(7, owner.DispatcherId!), CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.Applied);
		(await store.GetAllTenantsFailedMessagesAsync(100, null, 10, CancellationToken.None))
			.ShouldContain(m => m.Id == msg.Id, "the reservation owner must be able to record a failure");
	}
}
