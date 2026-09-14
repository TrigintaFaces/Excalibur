// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

using Excalibur.Data.CloudNative;
using Excalibur.Dispatch;
using Excalibur.Outbox.Diagnostics;

using FakeItEasy.Creation;

namespace Excalibur.Outbox.Tests.Diagnostics;

// Independent regression lock, author != implementer.
//
// THE PROPERTY, stated once and asserted over a list rather than once per capability:
//
//     for every capability C that the inner store implements, a decorated store must expose C;
//     and for every C the inner store does NOT implement, the decorated store must NOT advertise it.
//
// WHY IT IS SHAPED THIS WAY. The fenced-decorator pattern needs one subclass per capability — 2^N classes
// for N capabilities, and every one of them is a place to forget. The deleted FencedTelemetryOutboxStoreDecorator.Factory
// said so in its own remarks: "selecting a decorator by hand at a registration site is how a
// capability gets silently dropped: the wrapper compiles, the `is IFencedOutboxStore` probe returns false,
// and fencing disappears without an error anywhere."
//
// A lock written per capability inherits that cost and, worse, inherits the omission: the capability nobody
// remembered to forward is the capability nobody remembers to test. Telemetry's backoff forwarding was
// locked three times over; IOutboxStoreAdmin and IMultiTransportOutboxStore were locked zero times, and both
// ship stripped. This arm iterates the capability list, so the SIXTH capability — the one nobody has named
// yet — is covered on the day it is added to the list, without a new test.
//
// WHY NO EXISTING TEST CAUGHT THIS. A frozen capability matrix already exists —
// tests/conformance/.../OutboxCapabilityMatrixShould.cs, 19 capability entries over every concrete store —
// and its own summary says:
//
//     "One entry per concrete backing outbox store. Decorators (encrypting/telemetry) are intentionally
//      excluded — they wrap a store, they are not a backing store."
//
// That sentence is true, and it is the seam. The matrix freezes what each STORE implements; nothing asserted
// that a DECORATOR preserves what its inner store implements. The hole was documented years before anyone
// noticed it, and the documentation was mistaken for a decision. This file is the complement, not a duplicate.
//
// RED at HEAD, deliberately, on the four capabilities the factory does not preserve:
// IOutboxStoreAdmin, IMultiTransportOutboxStore, IMultiTransportOutboxStoreAdmin, ICloudNativeOutboxStoreBatch.
//
// The last of those was NOT found by this lock. Its capability list was hand-written, and a reviewer read the
// source and found a probe the list had missed — the same whitelist defect the lock exists to catch, one layer
// up. `CoverEveryCapabilityTheProductionCodeProbes` now polices the list against the source, so the next
// omission fails here instead of hiding.
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class EveryOutboxDecoratorPreservesCapabilitiesShould
{
	/// <summary>
	/// Every optional capability that a consumer discovers by probing the store it was handed.
	/// </summary>
	/// <remarks>
	/// These are exactly the interfaces read off the decorated instance with <c>is</c> / <c>as</c>.
	/// <c>ITransactionalOutboxWriter</c> is deliberately absent: it is resolved from DI off the raw store and
	/// never probed on a decorated object, so a decorator cannot strip it.
	/// </remarks>
	/// <summary>The single source of truth. The theory data and the coverage guard both read this.</summary>
	private static readonly Type[] CapabilityTypes =
	[
		typeof(IOutboxStoreAdmin),
		typeof(IMultiTransportOutboxStore),
		typeof(IMultiTransportOutboxStoreAdmin),
		typeof(IFencedOutboxStore),
		typeof(IFencedOutboxStoreDiagnostics),
		typeof(IOutboxStoreBatch),
		typeof(IDeadLetterableOutboxStore),
		typeof(IBackoffSchedulableOutboxStore),
		typeof(ICloudNativeOutboxStoreBatch),
		typeof(IClaimScopedOutboxStore),
		typeof(IFencedClaimScopedOutboxStore),
		typeof(IFencedDeadLetterableOutboxStore),
		typeof(IOutboxStoreCapabilities),
	];

	public static TheoryData<Type> ProbedCapabilities()
	{
		var data = new TheoryData<Type>();

		foreach (var capability in CapabilityTypes)
		{
			data.Add(capability);
		}

		return data;
	}

	/// <summary>
	/// The subset whose presence must MIRROR the inner store: absent on the inner ⇒ absent on the decorator.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This split is a finding, not a convenience.</b> My first version of this file asserted
	/// "never advertise what the inner lacks" over <i>every</i> capability, and it went RED on
	/// <c>IOutboxStoreBatch</c>, <c>IDeadLetterableOutboxStore</c> and <c>IBackoffSchedulableOutboxStore</c> —
	/// which the decorator declares unconditionally, on purpose, each with a documented degradation:
	/// </para>
	/// <list type="bullet">
	/// <item><description><c>IBackoffSchedulableOutboxStore</c> — <b>fail-open</b>: falls back to
	/// <c>MarkFailedAsync</c>, "so the decorator never regresses behavior relative to an undecorated store."</description></item>
	/// <item><description><c>IDeadLetterableOutboxStore</c> — <b>fail-loud</b>: throws
	/// <c>NotSupportedException</c>, because a silent fallback "would leave the message re-claimable forever."</description></item>
	/// <item><description><c>IOutboxStoreBatch</c> — <b>conditional forward</b> to a batch-capable inner.</description></item>
	/// </list>
	/// <para>
	/// Those three answer the probe with <see langword="true"/> and then degrade explicitly. The other three
	/// are not declared at all: no contract, no degradation, just an absence a consumer reads as "unsupported."
	/// **Two different contracts, and asserting one property over both is how a lock indicts correct code.**
	/// </para>
	/// </remarks>
	/// <summary>The single source of truth for the mirror property. The theory data and the classification guard both read this.</summary>
	private static readonly Type[] MirroredCapabilityTypes =
	[
		typeof(IOutboxStoreAdmin),
		typeof(IMultiTransportOutboxStore),
		typeof(IMultiTransportOutboxStoreAdmin),
		typeof(IFencedOutboxStore),
		typeof(IFencedOutboxStoreDiagnostics),

		// Forwarded rather than declared, so its presence is the inner store's presence: the decorator
		// answers the probe only when the store beneath it does. A consumer whose store lacks the combined
		// capability must SEE that absence, because the drain's fenced route is chosen by this probe and a
		// decorator advertising what the inner cannot honour would route a fenced completion into nothing.
		typeof(IFencedClaimScopedOutboxStore),

		// Same forwarded-not-declared shape: presence must be the inner store's presence, because the drain
		// chooses the fenced TERMINAL route from this probe, and a decorator advertising what the inner
		// cannot honour would route an irreversible transition into nothing.
		typeof(IFencedDeadLetterableOutboxStore),

		// Forwarded by the encrypting decorator (it is in ForwardableCapabilitySet) and carries no message
		// payload, so its presence must likewise be the inner store's presence. FOUND BY THE
		// CLASSIFICATION GUARD BELOW ON ITS FIRST RUN: it was locked and probed but asserted nowhere, so
		// nothing said what a decorated store over a non-claim-scoped inner must report. The drain picks
		// the claim-scoped completion route from this probe, and a decorator answering true over an inner
		// that cannot honour it sends a completion into nothing -- the same hazard as its fenced sibling
		// above, which was locked while this one was not.
		typeof(IClaimScopedOutboxStore),

		// Forwarded, and the mirror property matters here for a different reason than the others: this
		// capability reports on the STORE'S OWN SHAPE -- whether it tracks sent messages or deletes them on
		// send. A decorator that answers it for the store underneath is not merely advertising something it
		// cannot honour, it is giving a WRONG answer about someone else. Denied, it read as absent, and a
		// delete-on-sent store became indistinguishable from a sent-tracking one for every consumer who
		// enabled crypto-shredding.
		typeof(IOutboxStoreCapabilities),
	];

	public static TheoryData<Type> MirrorTheInnerCapabilities()
	{
		var data = new TheoryData<Type>();

		foreach (var capability in MirroredCapabilityTypes)
		{
			data.Add(capability);
		}

		return data;
	}

	/// <summary>
	/// SAFETY, with the capability set DERIVED FROM THE INNER INSTANCE — no list to forget.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The architect's ruling, and it is stronger than the theory below it. Every arm in this file that reads
	/// a hand-written list can only lock the capabilities somebody remembered to write down; this arm asks the
	/// inner store what it implements and requires the decorated store to expose all of it. **A capability
	/// added to a provider tomorrow is covered today, by a test nobody edits.**
	/// </para>
	/// <para>
	/// It reports the whole diff at once rather than failing on the first miss, so a fix can be scoped in one
	/// pass instead of four rebuild cycles.
	/// </para>
	/// </remarks>
	[Fact]
	public void PreserveEveryCapabilityTheInnerInstanceReports_WithoutConsultingAnyList()
	{
		var inner = FakeStoreWithEveryCapability();
		var decorated = new TelemetryOutboxStoreDecorator(inner);

		// The fake is a proxy; its interface set includes FakeItEasy plumbing and the base contract. Keep the
		// optional outbox capabilities: interfaces in the framework's own namespaces, minus the base type.
		var innerCapabilities = inner.GetType().GetInterfaces()
			.Where(static i => i != typeof(IOutboxStore))
			.Where(static i => i.Name.Contains("Outbox", StringComparison.Ordinal))
			.Where(static i => i.Namespace?.StartsWith("Excalibur.", StringComparison.Ordinal) == true)
			.ToList();

		innerCapabilities.Count.ShouldBeGreaterThanOrEqualTo(
			5,
			"The inner fake reports too few outbox capabilities. Its interface set, not a list, is the input to " +
			"this arm — if it collapsed, the assertion below passes trivially.");

		var dropped = innerCapabilities
			.Where(c => !CanDiscover(decorated, c))
			.Select(static c => c.Name)
			.OrderBy(static n => n, StringComparer.Ordinal)
			.ToList();

		dropped.ShouldBeEmpty(
			"The decorator silently dropped capabilities its inner store implements. A consumer probing the " +
			"instance it was handed (`store as X`) gets null and the feature does not exist — no throw, no log. " +
			"This arm consults no list: it asked the inner store.\n\n" +
			"SEVERITY IS NOT UNIFORM. Measured store-type implementers in src/ at the time of writing:\n" +
			"  IOutboxStoreAdmin 8 · IMultiTransportOutboxStore 1 · IMultiTransportOutboxStoreAdmin 1\n" +
			"      -> LIVE strips: a shipped provider has the capability and a decorated host loses it.\n" +
			"  ICloudNativeOutboxStoreBatch 0\n" +
			"      -> LATENT strips: really dropped, but no shipped store implements them, so no consumer can\n" +
			"         reach the capability to lose it. Fix them with the same wrapper; do not triage them as P0.\n\n" +
			"A latent drop is still a drop: the day a provider implements one, this arm is already RED and no\n" +
			"one had to remember to add it. That is why the fake implements capabilities nothing ships yet.\n\n" +
			"Dropped: " + string.Join(", ", dropped) + ".");
	}

	/// <summary>
	/// Can a consumer DISCOVER <paramref name="capability"/> on <paramref name="store"/>?
	/// </summary>
	/// <remarks>
	/// THE PROPERTY, NOT THE MECHANISM. These arms originally asked <c>capability.IsInstanceOfType(decorated)</c>
	/// — "is the decorator this type?". @SoftwareArchitect's SEC4 ruling shows that question is unsatisfiable:
	/// nine optional capability interfaces means 2^9 = 512 decorator variants, because an implementation is
	/// static and an inner's capability set is dynamic. The lock forbade the only sane fix.
	///
	/// The ruled seam is <c>IOutboxStore : IServiceProvider</c> — a store returns itself for capabilities it
	/// supports and <see langword="null"/> otherwise, and a decorator forwards <c>GetService</c> to its inner.
	/// One method, no variants. Precedent: <c>HttpContext.Features</c>, and ISP's own <c>GetService(Type)</c>
	/// escape hatch.
	///
	/// So the arms ask the question they always meant: CAN A CONSUMER DISCOVER THE CAPABILITY? Either mechanism
	/// answers it. This keeps the lock RED at HEAD (today's decorator neither declares nor forwards the four
	/// stripped capabilities) and lets the ruled fix turn it GREEN without a combinatorial explosion.
	///
	/// I wrote this warning into the sibling lock's header and then bound the mechanism anyway, forty lines away.
	/// A lock that asserts the implementation you imagined, rather than the property you need, dictates the fix.
	/// </remarks>
	private static bool CanDiscover(object store, Type capability) =>
		capability.IsInstanceOfType(store)
		|| (store as IServiceProvider)?.GetService(capability) is not null;

	/// <summary>
	/// SAFETY, per named capability. Kept alongside the derived arm because it names the offender in the test id.
	/// </summary>
	/// <remarks>
	/// RED at HEAD for <c>IOutboxStoreAdmin</c>, <c>IMultiTransportOutboxStore</c>,
	/// <c>IMultiTransportOutboxStoreAdmin</c> and <c>ICloudNativeOutboxStoreBatch</c>. Consumers probe with
	/// <c>as</c>, so the capability does not throw — it evaluates null and the feature silently does not exist.
	/// </remarks>
	[Theory]
	[MemberData(nameof(ProbedCapabilities))]
	public void PreserveEveryCapabilityTheInnerStoreImplements(Type capability)
	{
		var inner = FakeStoreWithEveryCapability();

		capability.IsInstanceOfType(inner).ShouldBeTrue(
			$"Fixture error: the fake inner store must implement {capability.Name} for this arm to mean " +
			"anything. If this fails, the arm below is vacuous.");

		var decorated = new TelemetryOutboxStoreDecorator(inner);

		CanDiscover(decorated, capability).ShouldBeTrue(
			$"The telemetry decorator does not expose {capability.Name}, though the store it wraps implements " +
			"it. Consumers probe the instance they were handed (`store as " + capability.Name + "`), get null, " +
			"and the feature silently does not exist — no throw, no log. Fix the decorator to forward the " +
			"capability; do not remove it from ProbedCapabilities unless no consumer probes it off a decorated " +
			"instance any more.");
	}

	/// <summary>
	/// LIVENESS. A capability the inner store lacks must NOT be advertised by the decorator.
	/// </summary>
	/// <remarks>
	/// Without this arm, the safety arm above is satisfied by a decorator that declares every interface
	/// unconditionally — which would lie: each call against a non-capable inner would throw or no-op, and the
	/// consumer would lose the honest <see langword="null"/> that says the capability is genuinely absent.
	/// Together the two arms forbid the naive fix and force a capability-aware wrapper.
	/// </remarks>
	[Theory]
	[MemberData(nameof(MirrorTheInnerCapabilities))]
	public void NotAdvertiseACapabilityTheInnerStoreLacks(Type capability)
	{
		// FIXTURE HONESTY (l0qpxo seam migration). A bare A.Fake<IOutboxStore>() answers GetService(object-returning)
		// with a NON-NULL FakeItEasy dummy, not null. Under the ruled seam, base OutboxStoreDecorator.GetService
		// forwards unknown capabilities straight to Inner.GetService with no type filter — so the dummy leaks
		// through as a phantom capability and this arm false-REDs against correct code. FakeStoreImplementing answers
		// GetService the way a real store does: itself for a capability it implements, null otherwise. This teaches
		// the fake the seam; it does not weaken the assertion (the verdict is still ShouldBeFalse).
		var inner = FakeStoreImplementing();

		capability.IsInstanceOfType(inner).ShouldBeFalse(
			$"Fixture error: the bare fake must NOT implement {capability.Name}.");

		var decorated = new TelemetryOutboxStoreDecorator(inner);

		CanDiscover(decorated, capability).ShouldBeFalse(
			$"The decorator advertises {capability.Name} over a store that cannot honor it. An honest null is " +
			"information: it tells the consumer the capability is genuinely absent. Declaring the interface " +
			"unconditionally converts a discoverable absence into a runtime failure.");
	}

	/// <summary>
	/// The formerly always-advertised capabilities must now MIRROR the inner, like every other capability.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>INVERTED, deliberately, and this is a contract change rather than a compile fix.</b> This arm used to
	/// require the decorator to keep advertising <c>IOutboxStoreBatch</c> and <c>IBackoffSchedulableOutboxStore</c>
	/// over ANY inner, on the reasoning that each "genuinely degrades" — backoff fail-opened to
	/// <c>MarkFailedAsync</c>, batch fell back to a per-id loop. That reasoning was sound while a decorator could
	/// declare interfaces unconditionally.
	/// </para>
	/// <para>
	/// The ruled seam removes the premise. Forwarding is opt-in, the default is empty, and resolution is
	/// fail-closed: a decorator answers <c>GetService</c> with a wrapper when the inner is capable and
	/// <see langword="null"/> when it is not. There is no longer a declaration on which to hang a degradation. The
	/// degradation did not disappear — it MOVED to the consumer that was always entitled to choose it
	/// (<c>OutboxProcessor.MarkFailedForClaimAsync:817</c>, <c>OutboxStoreExtensions</c>'s per-id
	/// loop). Advertising a capability and then quietly degrading inside the decorator hid that choice from the
	/// only component qualified to make it.
	/// </para>
	/// <para>
	/// The old arm existed to stop someone narrowing the declarations to satisfy the mirror-the-inner arms. That
	/// narrowing is now the ruling. So the arm is inverted rather than deleted: the same two capabilities are still
	/// named, still pinned, and a regression in either direction is still caught — only the required verdict
	/// flipped, on an explicit ruling, recorded here.
	/// </para>
	/// </remarks>
	[Theory]
	[InlineData(typeof(IOutboxStoreBatch))]
	[InlineData(typeof(IBackoffSchedulableOutboxStore))]
	public void MirrorTheInner_ForTheCapabilitiesThatUsedToBeAdvertisedUnconditionally(Type capability)
	{
		var plain = FakeStoreImplementing();

		capability.IsInstanceOfType(plain).ShouldBeFalse(
			$"Fixture error: the bare fake must not implement {capability.Name}.");

		CanDiscover(new TelemetryOutboxStoreDecorator(plain), capability).ShouldBeFalse(
			$"The decorator advertises {capability.Name} over an inner store that does not implement it. Under the " +
			"ruled fail-closed seam a consumer reads a non-null capability as a promise the operation will be " +
			"performed. The honest null routes the consumer to its own documented fallback instead.");

		// NON-VACUITY. `ShouldBeFalse` above is satisfied by a decorator that answers null to EVERYTHING. The same
		// capability, over a CAPABLE inner, must still be discoverable — otherwise the negative proves nothing.
		var capable = FakeStoreImplementing(b => b.Implements(capability));

		CanDiscover(new TelemetryOutboxStoreDecorator(capable), capability).ShouldBeTrue(
			$"A capable inner must still expose {capability.Name} through the decorator. If this is false, the " +
			"assertion above is vacuous: the decorator is reporting absence for every capability, not for an " +
			"absent one, and the mirror-the-inner property is not being tested at all.");
	}

	/// <summary>
	/// RED. Dead-lettering does not degrade — it THROWS — so advertising it over a non-capable inner is the
	/// exact lie <see cref="NotAdvertiseACapabilityTheInnerStoreLacks"/> forbids.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This arm exists because my own lock certified the defect it was written to catch.</b> I grouped three
	/// interfaces under one arm named "the degrading capabilities" and asserted a single verdict for all three.
	/// Two degrade. <c>MarkDeadLetteredAsync</c> does not:
	/// </para>
	/// <code>
	/// if (_inner is not IDeadLetterableOutboxStore deadLetterable)
	///     throw new NotSupportedException(...);   // TelemetryOutboxStoreDecorator:160
	/// </code>
	/// <para>
	/// Four <c>is</c>-probe sites exist in that decorator: three for batch, one for backoff, <b>none</b> for
	/// dead-lettering. The <c>throw</c> is deliberate and its comment argues fail-loud over silent-no-op — which
	/// is correct <i>given</i> the unconditional declaration. The declaration is the bug. An honest absence beats
	/// a loud throw the consumer never asked for, and it is what the sibling arm demands for
	/// <c>IOutboxStoreAdmin</c>.
	/// </para>
	/// <para>
	/// I wrote "dead-lettering fails loud" into the blessing arm's own message. I knew it threw and blessed it
	/// anyway. A lock is not a record of what the code does; it is a claim about what the code must do.
	/// </para>
	/// <para>
	/// GREEN when the decorator stops advertising dead-lettering it cannot honor — under the ruled
	/// <c>IServiceProvider</c> seam, by forwarding <c>GetService</c> to the inner and returning
	/// <see langword="null"/> when the inner is not dead-letterable.
	/// </para>
	/// </remarks>
	[Fact]
	public void NotAdvertiseDeadLettering_OverANonDeadLetterableInner()
	{
		// Honest fake (see NotAdvertiseACapabilityTheInnerStoreLacks). This arm passes even with a bare fake because
		// Telemetry's IDeadLetterableOutboxStore branch has an `is`-filter that rejects the dummy — but that is luck,
		// not design. Using the honest fake makes it robust to the same GetService-dummy leak the sibling arms hit.
		var plain = FakeStoreImplementing();

		typeof(IDeadLetterableOutboxStore).IsInstanceOfType(plain).ShouldBeFalse(
			"Fixture error: the bare fake must not implement IDeadLetterableOutboxStore.");

		var decorated = new TelemetryOutboxStoreDecorator(plain);

		CanDiscover(decorated, typeof(IDeadLetterableOutboxStore)).ShouldBeFalse(
			"The decorator advertises IDeadLetterableOutboxStore over a store that cannot honor it, and " +
			"MarkDeadLetteredAsync then throws NotSupportedException (TelemetryOutboxStoreDecorator:160). " +
			"A consumer that probes the capability, finds it, and calls it, gets an exception instead of the " +
			"honest null that would have told it the capability is genuinely absent. That is precisely what " +
			"NotAdvertiseACapabilityTheInnerStoreLacks forbids for IOutboxStoreAdmin — the same defect, blessed " +
			"forty lines away because I named the arm from the class declaration instead of the method body.");
	}

	/// <summary>
	/// The capability list is HAND-MAINTAINED, so it must be policed. Every capability the production code
	/// probes off an outbox store must appear here, or in the exclusion list with a written reason.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Without this arm the list is a whitelist — the exact defect the rest of this file exists to catch, one
	/// layer up. It already missed <c>ICloudNativeOutboxStoreBatch</c>, probed four times in
	/// <c>CloudNativeOutboxStoreExtensions</c> and declared by no decorator; a reviewer found it, not the test.
	/// </para>
	/// <para>
	/// Reflection cannot supply the population: most of these capabilities do <b>not</b> derive
	/// <c>IOutboxStore</c> (only three do), so "interfaces assignable to IOutboxStore" discovers the wrong set.
	/// The population is defined by <i>what the code probes</i>, which is a fact about source text. So this arm
	/// reads the source.
	/// </para>
	/// </remarks>
	[Fact]
	public void CoverEveryCapabilityTheProductionCodeProbes()
	{
		var srcRoot = FindSourceRoot();

		var probed = Directory
			.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
			.Where(static p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
							&& !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.SelectMany(p => ProbePattern.Matches(File.ReadAllText(p)))
			.Select(m => m.Groups["capability"].Value)
			// IOutboxStore is the base contract, not an optional capability. `x is IOutboxStore` is a type
			// test, not a capability probe, and a decorator that failed it would not compile.
			.Where(static name => !string.Equals(name, nameof(IOutboxStore), StringComparison.Ordinal))
			.ToHashSet(StringComparer.Ordinal);

		// Non-vacuity: the scan must see a probe we know exists. A scan that matches nothing makes the
		// coverage check below pass trivially.
		probed.ShouldContain(
			nameof(IBackoffSchedulableOutboxStore),
			"The source scan found no probe for a capability the code demonstrably probes " +
			"(OutboxProcessor). The pattern or the source root has drifted and this arm is vacuous.");

		var locked = CapabilityTypes.Select(static t => t.Name);
		var known = locked.Concat(DocumentedExclusions).ToHashSet(StringComparer.Ordinal);

		var unlocked = probed.Where(p => !known.Contains(p)).OrderBy(static p => p, StringComparer.Ordinal).ToList();

		unlocked.ShouldBeEmpty(
			"These capabilities are probed off an outbox store in production code but are neither locked by " +
			"this file nor listed in DocumentedExclusions with a reason. A capability nobody remembered to " +
			"lock is the capability that ships stripped. Add each to ProbedCapabilities (and to " +
			"MirrorTheInnerCapabilities if a consumer must see its absence), or exclude it with a written " +
			"justification. Unlocked: " + string.Join(", ", unlocked) + ".");
	}

	/// <summary>
	/// Every probed capability must EITHER mirror the inner store OR be listed as declared-unconditionally
	/// with its degradation. Forgetting to classify one must fail here rather than ship.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This closes the one list in this file that nothing enforced.</b> The probed population already
	/// defends itself: a source scan discovers every capability production code tests with <c>is</c>/<c>as</c>
	/// and fails when one is neither locked nor excluded. <c>MirrorTheInnerCapabilities</c> had no such
	/// arm — it was consumed only as theory data — so a capability could be locked, probed, forwarded, and
	/// silently omitted from the mirror property with nothing going red.
	/// </para>
	/// <para>
	/// <b>The omission is not cosmetic, and the fencing guarantee is what pays for it.</b> The mirror
	/// property is what stops a decorator advertising a capability its inner store cannot honour. The drain
	/// selects its fenced route from that probe, so a decorator that answers <see langword="true"/> over an
	/// inner store lacking the capability routes an irreversible completion into nothing — which is
	/// precisely the mutation a superseded leader is not allowed to perform.
	/// </para>
	/// <para>
	/// <b>The default is MIRROR.</b> A capability is exempt only by being written into the list below with
	/// a reason, so the failure mode of forgetting is a red test rather than a silent hole. That is the
	/// same shape as <c>DocumentedExclusions</c> above, applied to the list that lacked it.
	/// </para>
	/// </remarks>
	[Fact]
	public void Classify_every_probed_capability_as_mirrored_or_documented_as_unconditional()
	{
		var mirrored = MirroredCapabilityTypes
			.Select(static t => t.Name)
			.ToHashSet(StringComparer.Ordinal);

		// Non-vacuity: the mirror list must contain something we know is in it. An empty read would make
		// every capability look unclassified and the assertion below would fail for the wrong reason.
		mirrored.ShouldContain(
			nameof(IFencedOutboxStore),
			"The mirror list read back empty or malformed, so this arm is measuring nothing.");

		var unclassified = CapabilityTypes
			.Select(static t => t.Name)
			.Where(name => !mirrored.Contains(name)
						&& !DeclaredUnconditionally.Contains(name)
						&& !DeniedByIsolation.Contains(name))
			.OrderBy(static n => n, StringComparer.Ordinal)
			.ToList();

		unclassified.ShouldBeEmpty(
			"These capabilities are locked by this file but are neither asserted to MIRROR the inner store " +
			"nor documented as declared-unconditionally-with-a-degradation. A capability in neither list " +
			"has no asserted behaviour when the inner store lacks it, which is exactly how a decorator comes " +
			"to advertise something it cannot honour. Add each to MirrorTheInnerCapabilities, or to " +
			"DeclaredUnconditionally with the degradation it performs. Unclassified: "
			+ string.Join(", ", unclassified) + ".");
	}

	/// <summary>
	/// Capabilities the encrypting decorator declares UNCONDITIONALLY, each with the degradation it performs.
	/// </summary>
	/// <remarks>
	/// These answer the probe with <see langword="true"/> even when the inner store lacks the capability, so
	/// asserting the mirror property over them would indict correct code. Each entry states what it does
	/// instead, because an unconditional declaration with no documented degradation is indistinguishable
	/// from a bug.
	/// </remarks>
	/// <summary>
	/// Capabilities the ISOLATING (encrypting) decorator denies outright, with the measured reason.
	/// </summary>
	/// <remarks>
	/// <para>
	/// These cannot satisfy the mirror property because the two decorators under test answer them
	/// differently by design. The telemetry decorator derives from the base, whose <c>GetService</c>
	/// delegates to the inner store, so it mirrors. The encrypting decorator derives from the isolating
	/// one, which is deny-by-default: anything outside its forwardable set resolves to <see langword="null"/>
	/// even when the inner store has it.
	/// </para>
	/// <para>
	/// <b>Denial here is deliberate, not an oversight</b> -- but it is a real capability loss for a consumer
	/// who enables crypto-shredding, and whether denial is the right treatment rather than wrapping is
	/// tracked separately. This list records what the code does; it does not bless it.
	/// </para>
	/// </remarks>
	private static readonly HashSet<string> DeniedByIsolation = new(StringComparer.Ordinal)
	{
		// MEASURED, not assumed: absent from EncryptingOutboxStoreDecorator entirely (0 occurrences), so
		// the isolating deny-by-default applies. Its AddBatchAsync takes CloudOutboxMessage values, so
		// forwarding it unmediated would hand the inner store plaintext payloads -- the one thing the
		// encrypting decorator exists to prevent. It is denied rather than wrapped.
		nameof(ICloudNativeOutboxStoreBatch),
	};

	private static readonly HashSet<string> DeclaredUnconditionally = new(StringComparer.Ordinal)
	{
		// FAIL-OPEN: falls back to MarkFailedAsync, so the decorator never regresses behaviour relative to
		// an undecorated store.
		nameof(IBackoffSchedulableOutboxStore),

		// FAIL-LOUD: throws NotSupportedException. A silent fallback would leave the message re-claimable
		// forever, so the failure is made audible rather than absorbed.
		nameof(IDeadLetterableOutboxStore),

		// CONDITIONAL FORWARD to a batch-capable inner store.
		nameof(IOutboxStoreBatch),
	};

	/// <summary>Capabilities that are probed but deliberately not locked here, each with its reason.</summary>
	/// <remarks>
	/// An exclusion is a claim. Each one below was measured, not assumed — an earlier version of this list
	/// carried a reason that was simply false.
	/// </remarks>
	private static readonly string[] DocumentedExclusions =
	[
		// Probed off an IOutboxWriter, not off an outbox STORE, so the decorator hazard this file
		// exists to catch does not reach it: no writer decorator exists. Measured, not assumed --
		// the only IOutboxWriter implementations are DeferredOutboxWriter, which implements this
		// capability directly, and TransactionalOutboxWriter, which does not and silently drops the
		// scheduled time (tracked separately). Neither is wrapped; IOutboxWriter is registered
		// straight to the concrete type.
		//
		// THE EXCLUSION EXPIRES THE MOMENT A WRITER DECORATOR APPEARS. A decorator that forwards
		// IOutboxWriter without forwarding this capability would make every scheduled write
		// silently immediate -- the same stripping failure this file locks for stores. If one is
		// added, delete this entry and lock the capability instead.
		// A literal rather than nameof: the type is internal to Excalibur.Dispatch.Abstractions and
		// this assembly is not one of its friends, so the symbol is not in scope here.
		"IScheduledOutboxWriter",
	];

	/// <summary>Matches a capability probe (<c>is</c>/<c>as</c>) against an outbox-store capability interface.</summary>
	private static readonly Regex ProbePattern = new(
		@"\b(?:is|as)\s+(?<capability>I[A-Za-z]*Outbox[A-Za-z]*)\b",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	/// <summary>Walks up from the test output directory to the repository's <c>src</c> root.</summary>
	private static string FindSourceRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);

		while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
		{
			dir = dir.Parent;
		}

		dir.ShouldNotBeNull("Could not locate the repository's src/ directory from the test output path.");

		return Path.Combine(dir.FullName, "src");
	}

	/// <summary>
	/// NON-VACUITY. The capability list is non-empty and the factory really wraps.
	/// </summary>
	/// <remarks>
	/// A theory over an empty list passes without running. A factory that returned its argument unchanged
	/// would satisfy every arm above. Both are pinned here.
	/// </remarks>
	[Fact]
	public void HaveANonEmptyCapabilityList_AndAFactoryThatActuallyDecorates()
	{
		ProbedCapabilities().Count.ShouldBeGreaterThanOrEqualTo(
			4,
			"A theory over an empty or truncated capability list passes by never asking. If capabilities were " +
			"removed here, the arms above stopped constraining them.");

		var inner = A.Fake<IOutboxStore>();
		var decorated = new TelemetryOutboxStoreDecorator(inner);

		decorated.ShouldNotBeSameAs(
			inner,
			"Factory.Decorate returned its argument unchanged. Every arm in this file would then pass " +
			"vacuously, because the 'decorated' store is the inner store.");
	}

	/// <summary>
	/// Fencing, pinned so a regression is named rather than inferred. Both arms, because fencing is a safety
	/// control: losing it silently disables split-brain protection, and inventing it presents a token to a store
	/// that cannot enforce one.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The mechanism moved and this arm moved with it.</b> It used to read <c>ShouldBeAssignableTo</c> and
	/// <c>as IFencedOutboxStore</c> — a question about the decorator's own type. Under the ruled
	/// <c>IOutboxStore : IServiceProvider</c> seam the decorator declares no capability interfaces, so both stopped
	/// compiling, correctly. The property never changed: a consumer must be able to discover fencing through a
	/// decorated store exactly when the inner store can enforce it.
	/// </para>
	/// <para>
	/// <b>This arm is currently a lie by omission, and I will not pretend otherwise.</b> It proves the DECORATOR
	/// preserves fencing. It says nothing about the consumer. <c>OutboxProcessor:90</c> still reads
	/// <c>_outboxStore as IFencedOutboxStore</c>, which is null through any decorator, so fencing is silently
	/// disabled on every decorated deployment even when every assertion below is green. Reported to
	/// @ProjectManager / @SoftwareArchitect; the consumer-side lock belongs in a file I have not been scoped to
	/// write. A green here is not a green for fencing.
	/// </para>
	/// </remarks>
	[Fact]
	public void PreserveFencing_WhichIsTheOneCapabilityTheFactoryAlreadyHandles()
	{
		var fenced = FakeStoreImplementing(b => b.Implements<IFencedOutboxStore>());
		var plain = FakeStoreImplementing();

		CanDiscover(new TelemetryOutboxStoreDecorator(fenced), typeof(IFencedOutboxStore)).ShouldBeTrue(
			"A fenced inner store must yield a decorated store through which fencing is discoverable. If it does " +
			"not, OutboxProcessor stops presenting the leadership token and falls through to the unfenced claim " +
			"path — split-brain protection turns itself off with no throw and no log.");

		CanDiscover(new TelemetryOutboxStoreDecorator(plain), typeof(IFencedOutboxStore)).ShouldBeFalse(
			"An unfenced inner store must not yield a decorator advertising fencing — the processor would " +
			"present a token to a store that cannot enforce it.");
	}

	/// <summary>A fake store that answers <c>GetService</c> the way a real store does.</summary>
	/// <remarks>
	/// A bare FakeItEasy fake answers <c>GetService</c> with null for every type, including interfaces it
	/// demonstrably implements. Probing such a fake through the seam reports zero capabilities and indicts the
	/// decorator for a defect in this fixture. A false RED is exactly as dishonest as a false GREEN.
	/// </remarks>
	private static IOutboxStore FakeStoreImplementing(Action<IFakeOptions<IOutboxStore>>? configure = null)
	{
		var fake = configure is null ? A.Fake<IOutboxStore>() : A.Fake<IOutboxStore>(configure);

		A.CallTo(() => fake.GetService(A<Type>._))
			.ReturnsLazily((Type serviceType) => serviceType.IsInstanceOfType(fake) ? fake : null);

		return fake;
	}

	/// <summary>An inner store implementing every capability a consumer may probe for.</summary>
	/// <summary>
	/// A fake inner store that answers <c>GetService</c> the way a REAL store does.
	/// </summary>
	/// <remarks>
	/// A bare FakeItEasy fake returns <see langword="null"/> from every <c>GetService</c> call. Under the ruled
	/// <c>IOutboxStore : IServiceProvider</c> seam that makes the inner store report NO capabilities even though
	/// it implements eight of them — and every arm below would go RED blaming the decorator for a defect in this
	/// fixture. A false RED is exactly as dishonest as a false GREEN, and it is the easier of the two to publish
	/// because it looks like diligence.
	///
	/// So the fake behaves as `OutboxStoreDecorator` documents a store must: return yourself for a capability you
	/// implement, <see langword="null"/> otherwise.
	/// </remarks>
	private static IOutboxStore FakeStoreWithEveryCapability()
	{
		var fake = A.Fake<IOutboxStore>(b => b
			.Implements<IOutboxStoreAdmin>()
			.Implements<IMultiTransportOutboxStore>()
			.Implements<IMultiTransportOutboxStoreAdmin>()
			.Implements<IFencedOutboxStore>()
			.Implements<IFencedOutboxStoreDiagnostics>()
			.Implements<IOutboxStoreBatch>()
			.Implements<IDeadLetterableOutboxStore>()
			.Implements<IBackoffSchedulableOutboxStore>()
			.Implements<ICloudNativeOutboxStoreBatch>()
			.Implements<IClaimScopedOutboxStore>()
			.Implements<IFencedClaimScopedOutboxStore>()
			.Implements<IFencedDeadLetterableOutboxStore>()
			.Implements<IOutboxStoreCapabilities>());

		A.CallTo(() => fake.GetService(A<Type>._))
			.ReturnsLazily((Type serviceType) => serviceType.IsInstanceOfType(fake) ? fake : null);

		return fake;
	}
}
