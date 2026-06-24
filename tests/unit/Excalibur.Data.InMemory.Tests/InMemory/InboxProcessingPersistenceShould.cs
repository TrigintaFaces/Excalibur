// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.InMemory;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.InMemory.Tests.InMemory;

// bd-dziy0x (S841, ADR-336): InboxMiddleware marked Processing in-memory only (InboxEntry.MarkProcessing() on a
// discarded local) — IInboxStore had no MarkProcessingAsync, so the durable row stayed Received for the whole
// handler run. The competing-consumer guard + stuck-processing timeout (which read the durable status) were
// therefore dead code → duplicate handler execution under concurrent delivery. The fix adds the segregated
// IProcessingTrackingInboxStore.MarkProcessingAsync, persisted BEFORE the handler. Independent engage-test
// (author≠impl): MarkProcessingAsync durably persists Processing, observable via GetEntryAsync (AC-5) by a
// second/competing reader (AC-5a-enabling). RED if MarkProcessingAsync is a no-op / mutates only a copy.
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
public sealed class InboxProcessingPersistenceShould
{
	private const string MsgId = "msg-1";
	private const string Handler = "TestHandler";

	private static InMemoryInboxStore CreateStore() =>
		new(Options.Create(new InMemoryInboxOptions()), NullLogger<InMemoryInboxStore>.Instance);

	private static async Task<InMemoryInboxStore> CreateStoreWithEntryAsync()
	{
		var store = CreateStore();
		_ = await store.CreateEntryAsync(
			MsgId, Handler, "TestMessageType", [1], new Dictionary<string, object>(StringComparer.Ordinal),
			CancellationToken.None);
		return store;
	}

	// AC-5 — MarkProcessingAsync durably persists Processing, readable via GetEntryAsync before the handler completes.
	[Fact]
	public async Task DurablyPersistProcessingStatus_ReadableViaGetEntry()
	{
		using var store = await CreateStoreWithEntryAsync();

		// Sanity: a freshly-created entry is Received (otherwise the lock would be vacuous).
		(await store.GetEntryAsync(MsgId, Handler, CancellationToken.None))!.Status.ShouldBe(InboxStatus.Received);

		await store.MarkProcessingAsync(MsgId, Handler, CancellationToken.None);

		var entry = await store.GetEntryAsync(MsgId, Handler, CancellationToken.None);
		entry.ShouldNotBeNull();
		entry!.Status.ShouldBe(InboxStatus.Processing);
	}

	// AC-5a (guard-enabling) — the durable Processing state is observable by a SECOND reader (the competing-
	// consumer guard reads GetEntryAsync → sees Processing → skips). Pre-fix the status stayed Received, so the
	// guard could never fire.
	[Fact]
	public async Task ExposeDurableProcessingToASecondReader()
	{
		using var store = await CreateStoreWithEntryAsync();
		await store.MarkProcessingAsync(MsgId, Handler, CancellationToken.None);

		var observedByCompetingConsumer = await store.GetEntryAsync(MsgId, Handler, CancellationToken.None);

		observedByCompetingConsumer.ShouldNotBeNull();
		observedByCompetingConsumer!.Status.ShouldBe(InboxStatus.Processing);
	}

	[Fact]
	public async Task ThrowWhenMarkingProcessingForUnknownEntry()
	{
		using var store = CreateStore();

		await Should.ThrowAsync<InvalidOperationException>(
			() => store.MarkProcessingAsync("unknown-message", Handler, CancellationToken.None).AsTask());
	}
}
