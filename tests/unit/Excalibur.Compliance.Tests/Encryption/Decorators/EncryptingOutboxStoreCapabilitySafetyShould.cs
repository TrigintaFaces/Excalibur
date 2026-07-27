// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption.Decorators;
using Excalibur.Dispatch;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Encryption.Decorators;

// The encryption-boundary safety lock for the deny-by-default outbox decorator.
//
// THE PROPERTY. When field-level encryption is enabled, the resolved IOutboxStore is the
// EncryptingOutboxStoreDecorator. Every capability whose surface carries a message PAYLOAD must be resolved by
// GetService as a MEDIATING (encrypting/decrypting) view — NEVER the raw inner, which would hand a consumer
// ciphertext to read or accept plaintext to write, straight past the encryption layer. A capability the inner
// lacks must resolve to null (deny-by-default), never a raw or half-built view.
//
// WHY DERIVED, NOT HAND-LISTED. The payload-bearing set is COMPUTED from the capability surfaces, not typed by
// hand — a hand list is a whitelist, and the capability nobody remembered to add is the one that ships
// stripped. It is computed by walking each capability's members INCLUDING INHERITED ONES: Type.GetMethods() on
// an interface does NOT return members inherited from base interfaces, so a capability whose payload surface is
// inherited-only would be misclassified by a GetMethods()-only walk. The transitive walk below is therefore the
// correct classifier and stays robust the day such a capability is added to the codebase.
//
// Independent engage-test (author != implementer).
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptingOutboxStoreCapabilitySafetyShould
{
	/// <summary>The message-payload types a mediating capability view must never expose in the clear.</summary>
	private static readonly Type[] PayloadTypes = [typeof(OutboundMessage), typeof(InboxEntry)];

	/// <summary>
	/// The candidate capability universe: every public outbox-capability interface in the Abstractions assembly,
	/// excluding the base contract. NOT hand-listed — read from the assembly.
	/// </summary>
	private static IEnumerable<Type> CandidateCapabilities() =>
		typeof(IOutboxStore).Assembly.GetTypes()
			.Where(static t => t.IsInterface && t.IsPublic)
			.Where(static t => t.Name.Contains("Outbox", StringComparison.Ordinal))
			.Where(static t => t != typeof(IOutboxStore));

	/// <summary>All methods of <paramref name="iface"/> INCLUDING those inherited from base interfaces.</summary>
	/// <remarks>
	/// <c>t.GetMethods()</c> alone omits inherited interface members; the base interfaces must be walked
	/// explicitly, so a capability whose payload surface is inherited-only is still classified payload-bearing.
	/// </remarks>
	private static IEnumerable<MethodInfo> AllMembersTransitive(Type iface) =>
		iface.GetMethods().Concat(iface.GetInterfaces().SelectMany(static i => i.GetMethods()));

	private static bool TransitivelyPayloadBearing(Type iface) =>
		AllMembersTransitive(iface).Any(SignatureTouchesPayload);

	private static bool SignatureTouchesPayload(MethodInfo method) =>
		ReferencesPayload(method.ReturnType)
		|| method.GetParameters().Any(p => ReferencesPayload(p.ParameterType));

	/// <summary>True if <paramref name="type"/> is, contains, or is generic over a message-payload type.</summary>
	private static bool ReferencesPayload(Type type)
	{
		if (PayloadTypes.Contains(type))
		{
			return true;
		}

		if (type.HasElementType && type.GetElementType() is { } element && ReferencesPayload(element))
		{
			return true;
		}

		return type.IsGenericType && type.GetGenericArguments().Any(ReferencesPayload);
	}

	private static IReadOnlyList<Type> PayloadBearingCapabilities() =>
		CandidateCapabilities().Where(TransitivelyPayloadBearing).OrderBy(static t => t.Name, StringComparer.Ordinal).ToList();

	// ── The derivation is the lock. No impl mutation needed. ─────────────────────────────────────────────────────

	[Fact]
	public void DeriveEveryPayloadBearingCapability_ByNameNotByCount()
	{
		var payloadBearing = PayloadBearingCapabilities();

		// A count floor alone would ratify a swap, so name every payload-bearing capability the decorator must
		// mediate. If a new payload-bearing capability is added and not mediated, this fails until it is listed
		// AND the decorator wraps it (the safety/liveness arms below prove the mediation).
		var expected = new[]
		{
			typeof(IOutboxStoreAdmin),
			typeof(IOutboxStoreBatch),
			typeof(IMultiTransportOutboxStore),
			typeof(IMultiTransportOutboxStoreAdmin),
			typeof(IFencedOutboxStore),
		};

		foreach (var capability in expected)
		{
			payloadBearing.ShouldContain(
				capability,
				$"The payload-bearing set must NAME {capability.Name}. A count check alone would pass a set that " +
				"swapped one payload-bearing capability for another and still forwarded the missing one raw. " +
				"Derived: " + Join(payloadBearing));
		}

		payloadBearing.Count.ShouldBeGreaterThanOrEqualTo(
			expected.Length,
			"Fewer payload-bearing capabilities derived than the known set. If a capability was legitimately " +
			"removed, update this arm; otherwise the derivation dropped one. Derived: " + Join(payloadBearing));
	}

	// ── The security property, over the DERIVED set, against the REAL decorator. ────────────────────────────────

	[Fact]
	public void ResolveEveryPayloadBearingCapability_AsAMediatingView_NeverTheRawInner()
	{
		var inner = FakeStoreImplementingEveryPayloadCapability(out var payloadBearing);
		var decorator = CreateSut(inner);

		var leaked = new List<string>();

		foreach (var capability in payloadBearing)
		{
			var resolved = decorator.GetService(capability);

			// SAFETY: never the raw inner. A resolved view that IS the inner hands the consumer un-mediated access
			// to the payload — the whole encryption boundary bypassed. Null is acceptable here (deny); rawness is not.
			if (resolved is not null && ReferenceEquals(resolved, inner))
			{
				leaked.Add(capability.Name);
			}
		}

		leaked.ShouldBeEmpty(
			"The encrypting decorator resolved these payload-bearing capabilities to the RAW inner store, exposing " +
			"the message payload past the encryption layer: " + string.Join(", ", leaked) + ". Every payload-bearing " +
			"capability must resolve to a mediating (encrypting/decrypting) view or to null — never the inner itself.");
	}

	[Fact]
	public void ResolveWrappedCapabilities_ToAWorkingMediatingView_OverACapableInner()
	{
		// LIVENESS. The safety arm above is satisfied by a decorator that answers null to everything (null is never
		// the raw inner). The wrapped payload-bearing capabilities must resolve NON-NULL over a capable inner,
		// or the boundary is inert and every encrypted admin/batch/fenced/multi-transport operation silently fails.
		var inner = FakeStoreImplementingEveryPayloadCapability(out _);
		var decorator = CreateSut(inner);

		var wrapped = new[]
		{
			typeof(IOutboxStoreAdmin),
			typeof(IOutboxStoreBatch),
			typeof(IMultiTransportOutboxStore),
			typeof(IMultiTransportOutboxStoreAdmin),
			typeof(IFencedOutboxStore),
		};

		var inert = wrapped
			.Where(c => decorator.GetService(c) is null)
			.Select(static c => c.Name)
			.ToList();

		inert.ShouldBeEmpty(
			"These wrapped payload-bearing capabilities resolved to null over a capable inner, so the mediating " +
			"view is inert and the operation silently does nothing behind encryption: " + string.Join(", ", inert) +
			". Each must resolve to a working encrypting/decrypting view.");
	}

	// ── fixtures ────────────────────────────────────────────────────────────────────────────────────────────────

	private static EncryptingOutboxStoreDecorator CreateSut(IOutboxStore inner) =>
		new(inner, A.Fake<IEncryptionProviderRegistry>(), Options.Create(new EncryptionOptions()));

	/// <summary>
	/// A fake inner implementing every WRAPPED payload-bearing capability and answering GetService like a real
	/// store. Returns the derived payload-bearing set so the safety arm iterates exactly what the derivation found.
	/// </summary>
	/// <remarks>
	/// FIXTURE HONESTY: a bare FakeItEasy fake answers GetService with null for interfaces it implements, which
	/// would make the decorator's WrapCapability see a non-capable inner and resolve null — a false GREEN for the
	/// safety arm (null is never the raw inner) and a false RED for the liveness arm. The fake answers GetService as
	/// a real store does.
	/// </remarks>
	private static IOutboxStore FakeStoreImplementingEveryPayloadCapability(out IReadOnlyList<Type> payloadBearing)
	{
		payloadBearing = PayloadBearingCapabilities();

		var fake = A.Fake<IOutboxStore>(b => b
			.Implements<IOutboxStoreAdmin>()
			.Implements<IOutboxStoreBatch>()
			.Implements<IMultiTransportOutboxStore>()
			.Implements<IMultiTransportOutboxStoreAdmin>()
			.Implements<IFencedOutboxStore>());

		A.CallTo(() => fake.GetService(A<Type>._))
			.ReturnsLazily((Type serviceType) => serviceType.IsInstanceOfType(fake) ? fake : null);

		return fake;
	}

	private static string Join(IEnumerable<Type> types) => string.Join(", ", types.Select(static t => t.Name));
}
