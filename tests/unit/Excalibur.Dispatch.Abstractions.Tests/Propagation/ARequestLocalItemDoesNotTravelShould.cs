// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Features;

using Tests.Shared.TestDoubles;

namespace Excalibur.Dispatch.Abstractions.Tests.Propagation;

/// <summary>
/// Binds the boundary between the request-local item collection and the values a context carries beyond
/// the process.
/// </summary>
/// <remarks>
/// <para>
/// Every arm here is paired: an ABSENCE arm proving a write to <see cref="IMessageContext.Items"/> does
/// not reach a sink, and a PRESENCE arm proving a marked value does. Neither alone is evidence — an
/// absence arm passes against a sink that writes nothing at all, and a presence arm passes against one
/// that writes everything.
/// </para>
/// <para>
/// The presence arms use a key no other code path in this framework can produce. Marking
/// <c>correlationid</c>, <c>traceparent</c>, a <c>baggage.</c> entry or a known metadata key would
/// prove that the context carries its own fields, which it already did, rather than that propagation
/// works.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class ARequestLocalItemDoesNotTravelShould
{
	// Not produced anywhere else in src/: not a header spelling, not a metadata key, not a baggage prefix.
	private const string MarkedKey = "zzpropagationprobe";
	private const string MarkedValue = "probe-value";

	private static readonly byte[] Payload = [1, 2, 3];

	[Fact]
	public void KeepARequestLocalItemOutOfTheStagedOutboxHeaders()
	{
		// This is the leg that actually round-tripped. The SQL Server store handed the whole item
		// collection to the staging factory and the dequeue mapper replayed each entry with the KEY
		// UNCHANGED, so an item named for an inbound HTTP header reached the broker as that header.
		var context = new TestMessageContext();
		context.Items["X-Request-Id"] = "req-1";
		context.Items["Authorization"] = "Bearer leaked";

		var staged = OutboundMessage.FromContext("T", Payload, "d", context);

		staged.Headers.ShouldNotContainKey("X-Request-Id");
		staged.Headers.ShouldNotContainKey("Authorization");
	}

	[Fact]
	public void StageAMarkedValueInTheOutboxHeaders()
	{
		var context = new TestMessageContext();
		context.SetPropagatedItem(MarkedKey, MarkedValue);

		var staged = OutboundMessage.FromContext("T", Payload, "d", context);

		staged.Headers[MarkedKey].ShouldBe(MarkedValue);
	}

	[Fact]
	public void StageAMarkedValueForAStoreThatSuppliesNoHeadersOfItsOwn()
	{
		// Seven of the nine outbox stores call this factory with no headers argument at all. While the
		// propagated set arrived THROUGH that optional parameter, a marked value reached the broker on
		// the providers that passed it and was silently dropped by the rest — the same consumer code
		// behaving differently per provider, with nothing downstream able to tell which happened.
		var context = new TestMessageContext();
		context.SetPropagatedItem(MarkedKey, MarkedValue);

		var staged = OutboundMessage.FromContext("T", Payload, "d", context, headers: null);

		staged.Headers.ShouldContainKeyAndValue(MarkedKey, MarkedValue);
	}

	[Theory]
	[InlineData("MessageId")]
	[InlineData("CorrelationId")]
	[InlineData("CausationId")]
	[InlineData("MessageType")]
	[InlineData("Timestamp")]
	public void RefuseAKeyAnOutboxStoreSuppliesAsItsOwnEnvelopeFraming(string reserved)
	{
		// Two of the nine stores supply exactly these five; the other seven supply none. While marking
		// one was permitted, the consumer's value reached the broker on the seven and was silently
		// replaced by the store's on the two -- the same code with a different observable result per
		// provider, which is this change's own defect sentence in different keys. Precedence resolved
		// the collision; refusal makes it inexpressible, at the line the consumer wrote rather than two
		// providers later.
		var context = new TestMessageContext();

		var thrown = Should.Throw<ArgumentException>(
			() => context.SetPropagatedItem(reserved, "consumer-supplied"));

		thrown.Message.ShouldContain("reserved");
		context.GetPropagatedItems().ShouldBeEmpty();
	}

	[Fact]
	public void StillLetAStoresOwnEnvelopeKeyWinIfOneReachesTheMergeAnyway()
	{
		// Liveness for the overlay the refusal above makes unreachable for a collision. The overlay is a
		// rule and the reserved set is a list, so the rule is kept and bound: a store's framing must not
		// be forgeable by consumer data even if a key is added to one and not the other.
		var context = new TestMessageContext();
		context.SetPropagatedItem(MarkedKey, MarkedValue);

		var staged = OutboundMessage.FromContext(
			"T",
			Payload,
			"d",
			context,
			new Dictionary<string, object>(StringComparer.Ordinal) { [MarkedKey] = "store-supplied" });

		staged.Headers[MarkedKey].ShouldBe("store-supplied");
	}

	[Fact]
	public void NotMutateTheHeaderDictionaryTheCallerSupplied()
	{
		// Otherwise this factory's effect would depend on whether the caller reuses its dictionary, and
		// a store that stages two messages from one bag would carry the first message's marked values
		// into the second.
		var context = new TestMessageContext();
		context.SetPropagatedItem(MarkedKey, MarkedValue);
		var callerHeaders = new Dictionary<string, object>(StringComparer.Ordinal) { ["MessageType"] = "T" };

		_ = OutboundMessage.FromContext("T", Payload, "d", context, callerHeaders);

		callerHeaders.ShouldNotContainKey(MarkedKey);
		callerHeaders.Count.ShouldBe(1);
	}

	[Fact]
	public void NotAliasTheCallersHeaderDictionaryWhenNothingWasMarked()
	{
		// The half the arm above could not see, and the one on the COMMON path: while the factory
		// short-circuited when nothing was marked, it handed the caller's dictionary straight to the
		// constructor, so the staged message aliased it and a later write by the caller changed a
		// message already staged. An arm about marked values is structurally blind to this.
		var context = new TestMessageContext();
		var callerHeaders = new Dictionary<string, object>(StringComparer.Ordinal) { ["Timestamp"] = "t0" };

		var staged = OutboundMessage.FromContext("T", Payload, "d", context, callerHeaders);
		callerHeaders["Timestamp"] = "t1";

		staged.Headers["Timestamp"].ShouldBe("t0");
	}

	[Fact]
	public void RefuseToBeMutatedThroughACastToTheWritableInterface()
	{
		// IReadOnlyDictionary is NOT a read-only guarantee in .NET: Dictionary also implements
		// IDictionary, so returning the backing dictionary behind a read-only declared type publishes a
		// mutable collection and the annotation is a comment. A cast from any assembly, needing no
		// internals access, could clear the marked set or forge an entry past the reserved-name check.
		var context = new TestMessageContext();
		context.SetPropagatedItem(MarkedKey, MarkedValue);

		var view = context.GetPropagatedItems();

		_ = Should.Throw<NotSupportedException>(() => ((IDictionary<string, string>)view).Clear());
		_ = Should.Throw<NotSupportedException>(
			() => ((IDictionary<string, string>)view)["MessageType"] = "forged");
		view[MarkedKey].ShouldBe(MarkedValue);
	}

	[Fact]
	public void NotLetOneContextsEmptySetBecomeObservableInAnother()
	{
		// The empty case is where a shared instance is most tempting and most dangerous: it is handed to
		// EVERY context that marked nothing. A mutable one would be a process-global shared collection,
		// so one cast would make an entry no consumer placed anywhere appear as propagated on every
		// later unmarked context -- and be staged onto messages belonging to other tenants until the
		// process restarted. Strictly worse than the leak this change removes.
		var first = new TestMessageContext();
		var second = new TestMessageContext();

		var firstView = first.GetPropagatedItems();
		_ = Should.Throw<NotSupportedException>(
			() => ((IDictionary<string, string>)firstView).Add("poison", "x"));

		first.GetPropagatedItems().ShouldBeEmpty();
		second.GetPropagatedItems().ShouldBeEmpty();
		OutboundMessage.FromContext("T", Payload, "d", second).Headers.ShouldNotContainKey("poison");
	}

	[Fact]
	public void RefuseLoudlyWhenAContextBreaksTheFeaturesStabilityContract()
	{
		// The arm the first version of this suite NAMED as the hazard and did not construct.
		// IMessageContext.Features must return the same instance on every call; a consumer type
		// returning a fresh collection per call compiles and satisfies the signature, and a marked value
		// then lands in a discarded dictionary. Silent loss is this framework's catastrophic class, so
		// the surface detects it and fails at the consumer's own line -- a decision rather than a
		// discovery.
		IMessageContext unstable = new FreshFeaturesEveryCallContext();

		var thrown = Should.Throw<InvalidOperationException>(
			() => unstable.SetPropagatedItem(MarkedKey, MarkedValue));

		thrown.Message.ShouldContain("same instance on every call");
	}

	/// <summary>
	/// Deliberately violates the stability contract on <see cref="IMessageContext.Features" />, to prove
	/// the violation is detected rather than absorbed. Implements the interface directly, so no
	/// first-party base supplies the member under test.
	/// </summary>
	private sealed class FreshFeaturesEveryCallContext : IMessageContext
	{
		private readonly Dictionary<string, object> _items = new(StringComparer.Ordinal);

		public string? MessageId { get; set; }

		public string? CorrelationId { get; set; }

		public string? CausationId { get; set; }

		public IDispatchMessage? Message { get; set; }

		public object? Result { get; set; }

		public IServiceProvider RequestServices { get; set; } = null!;

		public IDictionary<string, object> Items => _items;

		public IDictionary<Type, object> Features => new Dictionary<Type, object>();
	}

	[Fact]
	public void CarryMarkedValuesIntoAChildContextWithoutSharingTheCollection()
	{
		// A shared reference would make the child's write mutate the parent retroactively, after a sink
		// may already have read the parent's set.
		var parent = new TestMessageContext();
		parent.SetPropagatedItem(MarkedKey, MarkedValue);

		var child = parent.CreateChildContext();
		child.SetPropagatedItem("zzchildonly", "child-value");

		child.GetPropagatedItems().ShouldContainKeyAndValue(MarkedKey, MarkedValue);
		parent.GetPropagatedItems().ShouldNotContainKey("zzchildonly");
	}

	[Fact]
	public void KeepARequestLocalItemOutOfAChildsPropagatedSet()
	{
		var parent = new TestMessageContext();
		parent.Items[MarkedKey] = MarkedValue;

		var child = parent.CreateChildContext();

		child.GetPropagatedItems().ShouldBeEmpty();
		child.Items.ShouldContainKey(MarkedKey);
	}

	[Fact]
	public void CarryAMarkedValueThroughAContextThisAssemblyDidNotWrite()
	{
		// The stability invariant on Items/Features — the same collection instance on every call — is a
		// property of the IMPLEMENTATION, not of the interface signature. A suite whose contexts are all
		// MessageContext asserts it in prose and checks it nowhere: a consumer type returning a fresh
		// collection per call compiles, and under default-deny it fails SILENTLY rather than loudly.
		// TestMessageContext and the child context below are two implementations neither of which is
		// MessageContext.
		IMessageContext foreign = new TestMessageContext();
		foreign.SetPropagatedItem(MarkedKey, MarkedValue);

		var staged = OutboundMessage.FromContext("T", Payload, "d", foreign);

		staged.Headers.ShouldContainKeyAndValue(MarkedKey, MarkedValue);

		IMessageContext child = foreign.CreateChildContext();
		child.GetPropagatedItem(MarkedKey).ShouldBe(MarkedValue);
	}

	[Fact]
	public void ReturnAnEmptySetRatherThanNullWhenNothingWasMarked()
	{
		var context = new TestMessageContext();
		context.Items["local"] = "value";

		context.GetPropagatedItems().ShouldNotBeNull();
		context.GetPropagatedItems().ShouldBeEmpty();
		context.GetPropagatedItem("local").ShouldBeNull();
	}

	[Fact]
	public void ReplaceAnEarlierValueForTheSameKey()
	{
		var context = new TestMessageContext();
		context.SetPropagatedItem(MarkedKey, "first");
		context.SetPropagatedItem(MarkedKey, "second");

		context.GetPropagatedItem(MarkedKey).ShouldBe("second");
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void RefuseAKeyThatNamesNothing(string key)
	{
		var context = new TestMessageContext();

		_ = Should.Throw<ArgumentException>(() => context.SetPropagatedItem(key, MarkedValue));
	}
}
