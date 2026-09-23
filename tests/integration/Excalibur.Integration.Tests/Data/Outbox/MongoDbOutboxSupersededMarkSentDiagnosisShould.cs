// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Outbox.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Lock on the property that replaced this file's original subject: MongoDB does not offer a fenced
/// outbox capability at all, so a superseded tenure has no member through which to complete work it no
/// longer owns.
/// </summary>
/// <remarks>
/// <para>
/// <b>This file previously asserted WHICH refusal a superseded leader received</b> — that it was told its
/// token was STALE rather than that the message was ALREADY SENT, because the two route differently and
/// the wrong one causes a delivered message to be recorded as failed by the loser of a leadership race.
/// That arm was correct and is no longer expressible: the member it exercised has been removed.
/// </para>
/// <para>
/// <b>Why the member was removed rather than repaired.</b> A fence is two properties, not one: the
/// comparison and the mutation must be a single atomic action, <em>and</em> the high-water must be
/// monotone across every fault in the deployment's model. MongoDB can be given the first — but the fence
/// document lives in a different collection from the message, so a single-document conditional update
/// cannot express the guard, and the store's own contract advertises that it works on standalone MongoDB
/// with no transaction or replica set. A store that satisfies atomicity and not durability meets the
/// letter of the fenced contract and is still unsafe.
/// </para>
/// <para>
/// <b>So the guarantee is now structural rather than tested.</b> The old arm asked "when a superseded
/// tenure presents a stale token, is it refused correctly?" — a question that presupposes the call is
/// possible. The question that survives is "can it present one at all?", and the answer must be no. That
/// is a stronger property: a refusal can regress, an absent member cannot.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> The arm below is RED the moment <c>MongoDbOutboxStore</c> re-declares
/// <see cref="IFencedOutboxStore"/> — which is exactly the regression it exists to catch, and the reason
/// it probes through <c>GetService</c> rather than asserting on the type. The liveness arm proves the
/// store still answers for the capabilities it genuinely has, so "returns null to everything" cannot pass.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Database", "MongoDb")]
public sealed class MongoDbOutboxSupersededMarkSentDiagnosisShould
{
	/// <summary>
	/// SAFETY: the fenced capability is not offered, so the superseded-tenure path is unreachable.
	/// </summary>
	[Fact]
	public void NotOfferAFencedCapabilityAtAll()
	{
		var store = NewStore();

		((IServiceProvider)store).GetService(typeof(IFencedOutboxStore)).ShouldBeNull(
			"MongoDB cannot keep a fence: its high-water is not monotone under failover, so offering the "
			+ "member would satisfy the contract's letter while leaving a superseded tenure able to "
			+ "complete work it no longer owns");
	}

	/// <summary>
	/// SAFETY: the same holds for the claim-scoped fenced contract, which is the one that demands the
	/// fence, the claim and the mutation be a single atomic action.
	/// </summary>
	[Fact]
	public void NotOfferTheClaimScopedFencedCapabilityEither()
	{
		var store = NewStore();

		((IServiceProvider)store).GetService(typeof(IFencedClaimScopedOutboxStore)).ShouldBeNull();
	}

	/// <summary>
	/// SAFETY: nor does it offer the fencing diagnostics, whose high-water nothing on this store advances.
	/// </summary>
	/// <remarks>
	/// With no fenced write path, reading that value reports a number no write ever consulted, and resetting it
	/// changes nothing that governs a write. Offering the member would present an operator with a control that
	/// looks like it protects the store and does not.
	/// </remarks>
	[Fact]
	public void NotOfferTheFencingDiagnosticsEither()
	{
		var store = NewStore();

		((IServiceProvider)store).GetService(typeof(IFencedOutboxStoreDiagnostics)).ShouldBeNull(
			"the store does not fence, so a fencing high-water it reports or resets governs nothing");
	}

	/// <summary>
	/// LIVENESS: the store still answers for a capability it genuinely has.
	/// </summary>
	/// <remarks>
	/// Without this, a store whose <c>GetService</c> returned null for everything — or one that failed to
	/// construct — would pass both arms above. This is the arm that makes their nulls mean something.
	/// </remarks>
	[Fact]
	public void StillOfferTheCapabilitiesItDoesHave()
	{
		var store = NewStore();

		((IServiceProvider)store).GetService(typeof(IOutboxStoreAdmin)).ShouldNotBeNull(
			"the store still implements the admin surface, so a null here would mean the probe itself is "
			+ "broken rather than the fenced capability being absent");
	}

	private static MongoDbOutboxStore NewStore()
	{
		// No container: every arm here is a capability-surface question, answered by construction and type
		// identity rather than by any round trip. Requiring real Mongo would make the lock skippable for a
		// property that has nothing to do with the server.
		var options = Options.Create(new MongoDbOutboxOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "excalibur-capability-probe",
		});

		return new MongoDbOutboxStore(options, NullLogger<MongoDbOutboxStore>.Instance);
	}
}
