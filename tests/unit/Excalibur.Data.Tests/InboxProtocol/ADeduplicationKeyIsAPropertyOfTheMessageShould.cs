// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware.Inbox;
using Excalibur.Dispatch.Options.Configuration;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.InboxProtocol;

/// <summary>
/// Binds the identity the inbox deduplicates on.
/// </summary>
/// <remarks>
/// <para>
/// The inbox turns at-least-once delivery into effectively-once processing of each
/// <c>(MessageId, HandlerType)</c>. That guarantee is worth nothing unless two deliveries of one message
/// resolve to the SAME key — <strong>a deduplication key must be a property of the MESSAGE, never of the
/// DELIVERY</strong> — and nothing stated that requirement, so nothing checked it.
/// </para>
/// <para>
/// What shipped: the resolver read <c>context.MessageId</c> first, and <c>MessageContext.MessageId</c>
/// mints a fresh GUID in its own getter when nobody assigned one. A context is built per dispatch, so a
/// redelivery resolved to a different key, no duplicate was detected, the handler ran again and the
/// dispatch reported success. Reading it first also made every other source unreachable, because a minted
/// value is never empty.
/// </para>
/// <para>
/// These arms observe the key BEHAVIOURALLY — the identifier the middleware actually asks the store
/// about — rather than calling the private resolver, so they bind what a consumer gets rather than how it
/// is computed. There were no arms on this resolver at all before these.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
public sealed class ADeduplicationKeyIsAPropertyOfTheMessageShould
{
	private const string KnownEventId = "zz-event-identity-1";
	private const string SharedCorrelation = "zz-shared-conversation";

	[Fact]
	public async Task NotUseAMintedContextIdentityAsTheDeduplicationKey()
	{
		// The headline. Nothing assigns MessageId, so reading it mints a GUID. The message carries its
		// own identity, and that is what the store must be asked about.
		var message = new ProbeEvent { EventId = KnownEventId };

		var asked = await ResolveKeyAsync(message, assignContextMessageId: null).ConfigureAwait(false);

		asked.ShouldBe(KnownEventId);
		Guid.TryParse(asked, out _).ShouldBeFalse(
			"a GUID here means the key came from the context's own lazy mint, which describes this "
			+ "delivery rather than this message.");
	}

	[Fact]
	public async Task ResolveTheSameKeyForTwoSeparateDeliveriesOfOneMessage()
	{
		// Invariant J1, asserted directly rather than inferred. Two deliveries means two contexts; the
		// key must not move. Every arm that supplies the key itself is blind to this, which is how the
		// defect survived a suite with real-infrastructure exactly-once coverage.
		var message = new ProbeEvent { EventId = KnownEventId };

		var first = await ResolveKeyAsync(message, assignContextMessageId: null).ConfigureAwait(false);
		var second = await ResolveKeyAsync(message, assignContextMessageId: null).ConfigureAwait(false);

		// Non-null FIRST, and this is not belt-and-braces. "Two deliveries resolve to the same key" is
		// satisfied by a resolver that resolves NOTHING -- null equals null -- so the equality alone is
		// vacuous. Found by a mutation that removed the identity arm and left this arm green while every
		// other arm reddened: the prediction was wrong, and the arm was the reason.
		first.ShouldBe(KnownEventId);
		second.ShouldBe(first);
	}

	[Fact]
	public async Task ResolveADomainEventsOwnEventIdRatherThanItsCorrelationId()
	{
		// A correlation id names a CONVERSATION. The resolver's reflective fallback looked for a
		// MessageId property and then for a CorrelationId property -- and DomainEvent exposes EventId,
		// CorrelationId and CausationId and no MessageId at all, so for a domain event it resolved the
		// conversation.
		var message = new ProbeEvent { EventId = KnownEventId, CorrelationOverride = SharedCorrelation };

		var asked = await ResolveKeyAsync(message, assignContextMessageId: null).ConfigureAwait(false);

		asked.ShouldBe(KnownEventId);
		asked.ShouldNotBe(SharedCorrelation);
	}

	[Fact]
	public async Task NotCollapseTwoDistinctEventsThatShareACorrelationId()
	{
		// Invariant J2, and the consumer-visible shape of the arm above: keying on the conversation made
		// the second event in it a duplicate of the first, so it was never handled and nothing recorded
		// that a message had been dropped.
		var first = new ProbeEvent { EventId = "zz-event-identity-A", CorrelationOverride = SharedCorrelation };
		var second = new ProbeEvent { EventId = "zz-event-identity-B", CorrelationOverride = SharedCorrelation };

		var firstKey = await ResolveKeyAsync(first, assignContextMessageId: null).ConfigureAwait(false);
		var secondKey = await ResolveKeyAsync(second, assignContextMessageId: null).ConfigureAwait(false);

		secondKey.ShouldNotBe(firstKey);
	}

	[Fact]
	public async Task UseAnAssignedContextIdentityWhenTheMessageCarriesNone()
	{
		// Liveness, and it is the arm that stops the others being satisfied by a resolver that returns
		// nothing. A message with no identity of its own must still be deduplicated on the identity the
		// wire deserializer assigned to the context.
		var asked = await ResolveKeyAsync(new ProbeCommand(), assignContextMessageId: "zz-wire-supplied-id")
			.ConfigureAwait(false);

		asked.ShouldBe("zz-wire-supplied-id");
	}

	[Fact]
	public async Task PreferTheMessagesOwnIdentityOverAnIdentityAssignedToTheContext()
	{
		// Ordering, and the reason it is this way round: two PUBLIC transport adapters assign a fresh
		// per-receive GUID to the context, so "somebody assigned this" does not distinguish a producer's
		// identity from a freshly invented one. A payload-borne identity survives a redelivery by
		// construction, so it outranks the context entirely rather than merely outranking a minted value.
		var message = new ProbeEvent { EventId = KnownEventId };

		var asked = await ResolveKeyAsync(message, assignContextMessageId: "zz-per-delivery-guid")
			.ConfigureAwait(false);

		asked.ShouldBe(KnownEventId);
	}

	[Fact]
	public async Task NotUseAMintedContextIdentityForAMessageThatCarriesNoIdentityOfItsOwn()
	{
		// This is the arm that binds the assigned-versus-minted distinction on its own. Every arm above
		// has a payload identity available, so the resolver never has to judge the context's value. Here
		// there is none: the message carries nothing and nothing was assigned, so the only candidate is
		// the GUID the context mints for itself -- which must be refused rather than used, because it
		// describes this delivery.
		//
		// The store is therefore never consulted and the message passes through undeduplicated. The
		// pass-through itself is unchanged and is still not the final intended behaviour -- but it is no
		// longer SILENT: the bypass is now recorded on a counter, on the current activity, and in a log
		// that names the message type, so a deployment can see which types are getting at-least-once.
		// The two arms below bind that. A refusal from this position remains unshipped deliberately,
		// because the dead-letter bridge is gated on retry exhaustion, so a bare failure here routes
		// nowhere by default and would convert a visible weaker guarantee into a silent dropped message.
		var asked = await ResolveKeyAsync(new ProbeCommand(), assignContextMessageId: null).ConfigureAwait(false);

		asked.ShouldBeNull(
			"a minted context identifier must never reach the store as a deduplication key: it is a "
			+ "property of this delivery, so a redelivery would produce a different one.");
	}

	/// <summary>
	/// Runs one message through the middleware against a real <see cref="MessageContext" /> and returns
	/// the identifier the middleware asked the store about.
	/// </summary>
	/// <remarks>
	/// A real <see cref="MessageContext" /> is load-bearing: a faked <see cref="IMessageContext" /> has no
	/// minting behaviour, so a suite built on fakes cannot reach the defect these arms exist for.
	/// </remarks>
	private static async Task<string?> ResolveKeyAsync(IDispatchMessage message, string? assignContextMessageId)
	{
		string? askedAbout = null;

		var store = A.Fake<IInboxStore>();
		_ = A.CallTo(() => store.GetEntryAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Invokes((string id, string _, CancellationToken _) => askedAbout = id)
			.Returns(new ValueTask<InboxEntry?>((InboxEntry?)null));

		var middleware = new InboxMiddleware(
			Options.Create(new InboxConfigurationOptions { Enabled = true }),
			store,
			deduplicator: null,
			new DispatchJsonSerializer(),
			NullLogger<InboxMiddleware>.Instance);

		var context = new MessageContext();
		if (assignContextMessageId is not null)
		{
			context.MessageId = assignContextMessageId;
		}

		_ = await middleware
			.InvokeAsync(
				message,
				context,
				(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
				CancellationToken.None)
			.ConfigureAwait(false);

		return askedAbout;
	}

	private sealed record ProbeEvent : DomainEvent
	{
		internal string? CorrelationOverride { get; init; }

		public override string? CorrelationId => CorrelationOverride;
	}

	private sealed record ProbeCommand : IDispatchMessage;

	/// <summary>
	/// SAFETY: a message the inbox cannot key is RECORDED as bypassing deduplication.
	/// </summary>
	/// <remarks>
	/// The pass-through was previously observable only as a log line with no message type in it, so an
	/// operator could learn that something had bypassed deduplication and never which thing. Nothing a
	/// monitoring system could alert on said that one message type was receiving a weaker delivery
	/// guarantee than the rest, which is what made this the silent class rather than merely a gap.
	/// </remarks>
	[Fact]
	public async Task RecordThatAnUnkeyableMessageBypassedDeduplication()
	{
		var measurements = await MeasureUnkeyedAsync(new ProbeCommand()).ConfigureAwait(false);

		measurements.Count.ShouldBe(
			1,
			"a message that passes through without deduplication must be counted, or the weaker "
			+ "guarantee it received is invisible to everything except a log reader.");

		measurements[0].Tags.ShouldContain(
			new KeyValuePair<string, object?>("inbox.message_type", nameof(ProbeCommand)),
			"the counter must name the TYPE. Without it an operator sees that something bypassed "
			+ "deduplication and cannot tell which message type to fix.");
	}

	/// <summary>
	/// LIVENESS: a message that DOES carry its own identity is not counted as a bypass.
	/// </summary>
	/// <remarks>
	/// Without this arm the one above is satisfied by a counter that fires on every message, which would
	/// report the whole deployment as undeduplicated and be ignored within a day.
	/// </remarks>
	[Fact]
	public async Task NotRecordABypassForAMessageThatCarriesItsOwnIdentity()
	{
		var measurements = await MeasureUnkeyedAsync(new ProbeEvent { EventId = KnownEventId })
			.ConfigureAwait(false);

		measurements.ShouldBeEmpty(
			"this message was deduplicated normally, so counting it as a bypass would make the signal "
			+ "useless -- a counter that fires for everything distinguishes nothing.");
	}

	// Collects the dispatch.inbox.unkeyed measurements emitted while one message is dispatched.
	private static async Task<List<(long Value, KeyValuePair<string, object?>[] Tags)>> MeasureUnkeyedAsync(
		IDispatchMessage message)
	{
		var measurements = new List<(long, KeyValuePair<string, object?>[])>();

		using var listener = new System.Diagnostics.Metrics.MeterListener();
		listener.InstrumentPublished = (instrument, l) =>
		{
			if (instrument.Name == "dispatch.inbox.unkeyed")
			{
				l.EnableMeasurementEvents(instrument);
			}
		};
		listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
			measurements.Add((value, tags.ToArray())));
		listener.Start();

		_ = await ResolveKeyAsync(message, assignContextMessageId: null).ConfigureAwait(false);

		listener.RecordObservableInstruments();
		return measurements;
	}
}
