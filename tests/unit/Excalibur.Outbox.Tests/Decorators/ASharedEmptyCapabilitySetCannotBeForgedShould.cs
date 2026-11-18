// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Outbox.Tests.Decorators;

/// <summary>
/// Binds the default forwardable-capability set against being mutated through a cast.
/// </summary>
/// <remarks>
/// <para>
/// <c>ForwardableCapabilities.Contains(serviceType)</c> is a security decision: it decides whether a
/// capability request reaches the <em>inner</em> store, bypassing the ISOLATION the decorator exists to
/// provide. The member is declared on <c>IsolatingOutboxStoreDecorator</c>, not on its base — the two
/// classes share one file, which is how an earlier pass attributed the site to the wrong one. Every decorator that does not override the property reads ONE SHARED INSTANCE, so
/// a mutable one would let a single cast — from any assembly, needing no internals access — add a type
/// that every such decorator then forwards, for the life of the process.
/// </para>
/// <para>
/// <see cref="IReadOnlySet{T}" /> is not a guarantee: <see cref="HashSet{T}" /> also implements
/// <see cref="ISet{T}" />, so returning one behind the read-only declared type publishes a mutable
/// collection and the annotation is a comment. The default is <c>FrozenSet&lt;Type&gt;.Empty</c>, whose
/// <see cref="ISet{T}" /> members throw, which is what closes the cast.
/// </para>
/// <para>
/// <strong>What would make these arms red:</strong> restoring the default to
/// <c>new HashSet&lt;Type&gt;()</c>. The cast then succeeds, <c>Add</c> returns rather than throwing, and
/// the forged type is observable through a second decorator. Both arms were written against that shape
/// and fail on it.
/// </para>
/// <para>
/// Found by sweeping for the SHAPE of a defect fixed on the message-context propagation surface rather
/// than for its symbol — a method returning <c>IReadOnly*</c> built directly from a mutable collection has
/// published a mutable collection. The same sweep found the sibling in the event-store isolating
/// decorator.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class ASharedEmptyCapabilitySetCannotBeForgedShould
{
	[Fact]
	public void RefuseToHaveACapabilityAddedThroughACastToTheWritableInterface()
	{
		var decorator = new ProbeDecorator(A.Fake<IOutboxStore>());

		var capabilities = decorator.Forwardable;

		_ = Should.Throw<NotSupportedException>(
			() => ((ISet<Type>)capabilities).Add(typeof(IProbeCapability)));
		capabilities.ShouldBeEmpty();
	}

	[Fact]
	public void NotLetOneDecoratorsForgedCapabilityBecomeVisibleToAnother()
	{
		// The shared-instance half, and the reason this is a process-wide defect rather than a local one:
		// a mutable default would make the forged entry visible to every decorator that did not override
		// the property, not only to the one the cast went through.
		var first = new ProbeDecorator(A.Fake<IOutboxStore>());
		var second = new ProbeDecorator(A.Fake<IOutboxStore>());

		_ = Should.Throw<NotSupportedException>(
			() => ((ISet<Type>)first.Forwardable).Add(typeof(IProbeCapability)));

		second.Forwardable.ShouldNotContain(typeof(IProbeCapability));
		second.Forwardable.ShouldBeEmpty();
	}

	[Fact]
	public void StillForwardACapabilityADecoratorDeliberatelyNames()
	{
		// Liveness. Both arms above are satisfied by a default that forwards nothing EVER — including for
		// a decorator that deliberately opts a capability in — which would make every opt-in inert while
		// each safety assertion passed. A decorator overriding the property must still be honoured.
		var decorator = new OptsInDecorator(A.Fake<IOutboxStore>());

		decorator.Forwardable.ShouldContain(typeof(IProbeCapability));
		decorator.Forwardable.Count.ShouldBe(1);
	}

	private interface IProbeCapability;

	private sealed class ProbeDecorator(IOutboxStore inner) : IsolatingOutboxStoreDecorator(inner)
	{
		internal IReadOnlySet<Type> Forwardable => ForwardableCapabilities;
	}

	private sealed class OptsInDecorator(IOutboxStore inner) : IsolatingOutboxStoreDecorator(inner)
	{
		private static readonly IReadOnlySet<Type> OptedIn =
			new HashSet<Type> { typeof(IProbeCapability) };

		internal IReadOnlySet<Type> Forwardable => ForwardableCapabilities;

		protected override IReadOnlySet<Type> ForwardableCapabilities => OptedIn;
	}
}
