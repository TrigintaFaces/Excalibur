// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Middleware.Inbox;
using Excalibur.Dispatch.Options.Configuration;
using Excalibur.Dispatch.Serialization;
using Excalibur.Inbox.DynamoDb;
using Excalibur.Inbox.ElasticSearch;
using Excalibur.Inbox.Firestore;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.InboxProtocol;

/// <summary>
/// The standard full-inbox path must select the self-expiring lease protocol only for stores that
/// actually implement it. Three shipped stores support the caller-governed claim and no lease; selecting
/// the lease branch for them raised on the first deduplicated message.
/// </summary>
/// <remarks>
/// <para>
/// The crash is now structurally unreachable — the lease protocol lives on its own interface, so a store
/// that does not implement it has no lease method to call. What remains reachable, and is what these
/// facts bind, is the middleware selecting the lease branch for a store that cannot serve it.
/// </para>
/// <para>
/// The stores are constructed offline. No provider infrastructure is required: the facts assert which
/// protocol a store offers and which branch the middleware takes, never a round-trip.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class LeaseClaimProtocolSupportShould : UnitTestBase
{
	public static TheoryData<string, IInboxStore> StoresWithoutALeasePath() => new()
	{
		{ "DynamoDb", CreateDynamoDbStore() },
		{ "Elasticsearch", CreateElasticsearchStore() },
		{ "Firestore", CreateFirestoreStore() },
	};

	/// <summary>
	/// The three stores offer the caller-governed claim and no lease. This pins the shape that
	/// <see cref="ClaimOnlyStore"/> models below, so the middleware facts are not asserted against a
	/// straw man: if a store ever gains or declares a lease path, this fact fails and the model is stale.
	/// </summary>
	[Theory]
	[MemberData(nameof(StoresWithoutALeasePath))]
	public void OfferTheClaimProtocolAndNoLease(string provider, IInboxStore store)
	{
		store.ShouldBeAssignableTo<IClaimableInboxStore>(
			$"{provider} is reached through the claim protocol.");

		store.ShouldNotBeAssignableTo<ILeasedInboxStore>(
			$"{provider} has no lease path. Declaring one it cannot serve is what put the standard inbox "
			+ "path onto a protocol the store does not implement.");
	}

	/// <summary>
	/// A store offering the claim protocol and no lease must be driven down the non-lease path.
	/// </summary>
	[Fact]
	public async Task NotTakeTheLeasePath_ForAClaimOnlyStore()
	{
		var store = new ClaimOnlyStore();

		_ = await ProcessOneMessageAsync(store).ConfigureAwait(false);

		store.LeaseAttempted.ShouldBeFalse(
			"a store with no lease path must never be asked for a lease.");
	}

	/// <summary>
	/// The gate reads the effective capability, not the declared interface. A decorator declares the
	/// capability interfaces so it can forward them, so a type check alone passes for a decorator whose
	/// inner store cannot serve the protocol — and the forward then fails at the first message.
	/// </summary>
	[Fact]
	public async Task NotTakeTheLeasePath_ForADeclaredLeaseOverANonLeasingInner()
	{
		var store = new LeaseDeclaringStore(effectivelyLeased: false);

		_ = await ProcessOneMessageAsync(store).ConfigureAwait(false);

		store.LeaseAttempted.ShouldBeFalse(
			"the store declares ILeasedInboxStore but reports SupportsLeasedClaim false, so the effective "
			+ "capability is absent. Gating on the declared interface alone selects it and forwards a "
			+ "lease into a store that cannot serve one.");
	}

	/// <summary>
	/// The liveness half: a store that genuinely leases MUST still be driven down the lease path, so the
	/// facts above cannot be satisfied by a gate that simply never selects a lease.
	/// </summary>
	[Fact]
	public async Task TakeTheLeasePath_ForAGenuinelyLeasingStore()
	{
		var store = new LeaseDeclaringStore(effectivelyLeased: true);

		_ = await ProcessOneMessageAsync(store).ConfigureAwait(false);

		store.LeaseAttempted.ShouldBeTrue(
			"a store that reports the lease capability must be admitted through the single atomic "
			+ "lease acquisition, not the check-then-act fallback.");
	}

	private static async Task<IMessageResult> ProcessOneMessageAsync(IInboxStore store)
	{
		var middleware = new InboxMiddleware(
			Options.Create(new InboxConfigurationOptions { Enabled = true }),
			store,
			deduplicator: null,
			new DispatchJsonSerializer(),
			NullLogger<InboxMiddleware>.Instance);

		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		_ = A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>());
		_ = A.CallTo(() => context.MessageId).Returns("lease-protocol-msg");

		return await middleware
			.InvokeAsync(A.Fake<IDispatchMessage>(), context, (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
				CancellationToken.None)
			.ConfigureAwait(false);
	}

	private static DynamoDbInboxStore CreateDynamoDbStore() =>
		new(
			Options.Create(new DynamoDbInboxOptions
			{
				TableName = "inbox",
				Connection = { ServiceUrl = "http://localhost:8000" },
			}),
			NullLogger<DynamoDbInboxStore>.Instance,
			UntenantedContext.Instance);

	private static ElasticsearchInboxStore CreateElasticsearchStore() =>
		new(
			new ElasticsearchClient(new Uri("http://localhost:9200")),
			Options.Create(new ElasticsearchInboxOptions()),
			NullLogger<ElasticsearchInboxStore>.Instance,
			UntenantedContext.Instance);

	private static FirestoreInboxStore CreateFirestoreStore() =>
		new(
			Options.Create(new FirestoreInboxOptions
			{
				ProjectId = "excalibur-repro",
				CollectionName = "inbox",
			}),
			NullLogger<FirestoreInboxStore>.Instance,
			UntenantedContext.Instance);
}
