// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Decorators;

namespace Excalibur.EventSourcing.Tests.Core.Decorators;

/// <summary>
/// Locks the erasure-capability probe on <see cref="IsolatingEventStoreDecorator"/>: it must answer for
/// what its inner chain can actually do, not for what its base class declares.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pre-fix.</b> The base declares <see cref="IEventStoreErasure"/> unconditionally — C# has no
/// conditional interface declaration — and implements none of its own; both members forward to the inner
/// store. The isolating decorator's sealed <c>GetService</c> tested <c>serviceType.IsInstanceOfType(this)</c>
/// first, which reads that declaration and is true for every subclass. So the probe answered "yes, I can
/// erase" over an inner store that cannot, and it did so <em>ahead of</em> the deny-by-default branch the
/// decorator exists to enforce — the one capability that bypassed it.
/// </para>
/// <para>
/// <b>Both directions are asserted.</b> The safety arm is that a decorator over a non-erasure store answers
/// <see langword="null"/>. The liveness arm is that a decorator over a store that <em>can</em> erase still
/// answers with itself, so the erase is reached THROUGH the decorator and its invariant cannot be bypassed —
/// without it, a <c>GetService</c> that returned <see langword="null"/> for everything would pass.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class IsolatingEventStoreDecoratorErasureProbeShould
{
	/// <summary>
	/// Safety, and the reported defect.
	/// </summary>
	[Fact]
	public void AnswerNullForTheErasureProbeOverAStoreThatCannotErase()
	{
		var inner = A.Fake<IEventStore>();
		A.CallTo(() => inner.GetService(typeof(IEventStoreErasure))).Returns(null);

		var sut = new TestIsolatingEventStore(inner);

		sut.GetService(typeof(IEventStoreErasure)).ShouldBeNull(
			"a decorator whose inner chain cannot erase must not claim the capability; the caller reads a "
			+ "non-null answer as a promise that the erase will be performed.");
	}

	/// <summary>
	/// Liveness. Without this, a probe that answered null for everything would satisfy the arm above.
	/// </summary>
	[Fact]
	public void AnswerWithItselfForTheErasureProbeOverAStoreThatCanErase()
	{
		var inner = A.Fake<IEventStore>();
		var erasure = A.Fake<IEventStoreErasure>();
		A.CallTo(() => inner.GetService(typeof(IEventStoreErasure))).Returns(erasure);

		var sut = new TestIsolatingEventStore(inner);

		sut.GetService(typeof(IEventStoreErasure)).ShouldBeSameAs(
			sut,
			"the erase must be reached through the decorator, not around it, so the decorator's invariant "
			+ "still applies to the erasure path.");
	}

	/// <summary>
	/// Liveness for deny-by-default. The erasure branch must not have reopened forwarding for anything else.
	/// </summary>
	[Fact]
	public void StillDenyAnUndeclaredCapabilityByDefault()
	{
		var inner = A.Fake<IEventStore>();
		A.CallTo(() => inner.GetService(typeof(IEventStoreArchive))).Returns(A.Fake<IEventStoreArchive>());

		var sut = new TestIsolatingEventStore(inner);

		sut.GetService(typeof(IEventStoreArchive)).ShouldBeNull(
			"a capability this decorator neither wraps nor declared forwardable stays unobtainable.");
	}

	/// <summary>
	/// A minimal isolating decorator: it declares nothing forwardable and wraps nothing, so every answer
	/// comes from the base behaviour under test.
	/// </summary>
	private sealed class TestIsolatingEventStore(IEventStore inner) : IsolatingEventStoreDecorator(inner);
}
