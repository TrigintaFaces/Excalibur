// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.InMemory;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.InMemory.Tests.InMemory;

/// <summary>
/// Binds the requirement that the in-memory inbox entry key is INJECTIVE in (tenant, message, handler).
/// </summary>
/// <remarks>
/// <para>
/// The three terms were joined as <c>{tenantId}:{messageId}:{handlerType}</c>. Neither the tenant term
/// nor the message id is validated against any charset -- both are caller data -- so tenant "a:b" with
/// message "c" and tenant "a" with message "b:c" both rendered "a:b:c:H" and occupied ONE entry.
/// </para>
/// <para>
/// A dedup collision does not throw. The second message is refused as already-seen and is never processed
/// and never retried: silent loss on the success path, and a cross-tenant isolation breach, since one
/// tenant's traffic then decides whether another's is delivered.
/// </para>
/// <para>
/// The key is in-process only and is never persisted, so no stored data is keyed by the old shape.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
public sealed class InboxCompositeKeyInjectivityShould
{
	private const string Handler = "OrderPlacedHandler";

	/// <summary>
	/// A tenant context whose tenant can be switched, so ONE store instance -- one entry dictionary -- can
	/// be addressed as two different tenants. The collision is only observable on a shared dictionary.
	/// </summary>
	private sealed class SwitchableTenantContext : ITenantContext
	{
		public string? TenantId { get; set; }

		public bool HasTenant => TenantId is not null;
	}

	private static InMemoryInboxStore CreateStore(SwitchableTenantContext tenant) =>
		new(Options.Create(new InMemoryInboxOptions()), NullLogger<InMemoryInboxStore>.Instance, tenant);

	private static ValueTask<InboxEntry> CreateEntryAsync(InMemoryInboxStore store, string messageId) =>
		store.CreateEntryAsync(
			messageId, Handler, "TestMessageType", [1],
			new Dictionary<string, object>(StringComparer.Ordinal), CancellationToken.None);

	/// <summary>
	/// SAFETY: the colliding pair -- a colon shifted across the tenant/message boundary.
	/// </summary>
	[Fact]
	public async Task NotTreatAShiftedColonTupleAsAnAlreadySeenMessage()
	{
		var tenant = new SwitchableTenantContext();
		using var store = CreateStore(tenant);

		// Tenant "a:b" receives message "c". Under the bare join this stored the key "a:b:c:H".
		tenant.TenantId = "a:b";
		_ = await CreateEntryAsync(store, "c");

		// A DIFFERENT tenant, "a", receives a DIFFERENT message, "b:c".
		// Under the bare join this composed the identical key, so the entry above answered for it.
		tenant.TenantId = "a";
		var seenByOtherTenant = await store.GetEntryAsync("b:c", Handler, CancellationToken.None);

		seenByOtherTenant.ShouldBeNull(
			"tenant 'a' message 'b:c' is a different message belonging to a different tenant than tenant "
			+ "'a:b' message 'c'. If the colon shifting across the boundary makes them share an entry, the "
			+ "second is refused as a duplicate and is never processed and never retried -- silent message "
			+ "loss, and one tenant's traffic deciding whether another's is delivered.");
	}

	/// <summary>
	/// LIVENESS: an ordinary message still keys, reads back, and is recognised as a duplicate.
	/// </summary>
	/// <remarks>
	/// Required. Without it the safety arm above is satisfied by a store that keys every write uniquely
	/// and finds nothing on read -- perfectly non-colliding, and it deduplicates nothing at all, turning
	/// the inbox from at-most-once into at-least-once for every message.
	/// </remarks>
	[Fact]
	public async Task StillStoreAndFindAnOrdinaryMessageForItsOwnTenant()
	{
		var tenant = new SwitchableTenantContext { TenantId = "tenant-7" };
		using var store = CreateStore(tenant);

		_ = await CreateEntryAsync(store, "order-42");

		var found = await store.GetEntryAsync("order-42", Handler, CancellationToken.None);

		found.ShouldNotBeNull(
			"an ordinary message must still be found by the tenant that received it, or the store "
			+ "recognises no duplicates at all");
	}

	/// <summary>
	/// LIVENESS: a tenant or message id that legitimately contains a colon must still key and read back.
	/// </summary>
	/// <remarks>
	/// The over-correction guard. A "fix" that rejected or stripped colons would pass the safety arm while
	/// breaking dedup for every consumer whose ids contain one -- a URN message id, for instance.
	/// </remarks>
	[Fact]
	public async Task StillFindAMessageWhoseTermsContainAColon()
	{
		var tenant = new SwitchableTenantContext { TenantId = "a:b" };
		using var store = CreateStore(tenant);

		_ = await CreateEntryAsync(store, "urn:uuid:9f8c");

		var found = await store.GetEntryAsync("urn:uuid:9f8c", Handler, CancellationToken.None);

		found.ShouldNotBeNull(
			"a colon is legal caller data in both terms. Making the key injective must not cost dedup for "
			+ "the messages whose ids contain one");
	}

	/// <summary>
	/// SAFETY: the ordinary cross-tenant case -- the same message id in two tenants stays two entries.
	/// </summary>
	[Fact]
	public async Task KeepTheSameMessageIdInTwoTenantsSeparate()
	{
		var tenant = new SwitchableTenantContext();
		using var store = CreateStore(tenant);

		tenant.TenantId = "tenant-7";
		_ = await CreateEntryAsync(store, "order-42");

		tenant.TenantId = "tenant-8";
		var seenByOtherTenant = await store.GetEntryAsync("order-42", Handler, CancellationToken.None);

		seenByOtherTenant.ShouldBeNull(
			"two tenants carrying the same message id are two distinct messages; the tenant term must "
			+ "remain part of the key");
	}
}
