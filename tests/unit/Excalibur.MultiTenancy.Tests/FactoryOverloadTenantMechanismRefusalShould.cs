// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// The factory overload of <c>AddTenantAwareStore</c> read a store's PUBLIC constructors to decide its
/// tenancy mechanism, and treated "no public constructor" as no ambient-scoped evidence — falling through
/// to <see cref="TenantMechanism.None"/> and emitting no capability marker at all.
/// </summary>
/// <remarks>
/// <para>
/// A factory constructs the store itself, so it can build a type whose constructors are all non-public.
/// That is not an exotic shape here: the internal-first standard makes it the expected one. So the probe
/// was reading an absence and reporting it as a decision. A genuinely ambient-scoped store registered that
/// way was recorded as having no tenancy mechanism, silently — the store isolates correctly and carries no
/// attestation that it does.
/// </para>
/// <para>
/// The property under test is that the seam REFUSES rather than guesses. A store that cannot be classified
/// fails its registration with a message naming both ways to declare a mechanism, so the outcome is a build
/// error a provider author reads, not a marker a consumer never receives.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class FactoryOverloadTenantMechanismRefusalShould
{
	private interface IProbeStore;

	[Fact]
	public void RefuseAStoreWhoseConstructorsAreAllNonPublic()
	{
		var services = new ServiceCollection();

		var thrown = Should.Throw<InvalidOperationException>(() =>
			services.AddTenantAwareStore<IProbeStore, NonPublicConstructorStore>(
				static _ => NonPublicConstructorStore.Create()));

		// The message has to tell a provider author what to do, not merely that something is wrong.
		thrown.Message.ShouldContain(nameof(ITenantContext));
		thrown.Message.ShouldContain(nameof(ITenantPartitionedStore));
	}

	[Fact]
	public void AcceptANonPublicConstructorStoreThatDeclaresRowPartitioning()
	{
		var services = new ServiceCollection();

		// Liveness's counterpart. The refusal must be about the ABSENCE OF A DECLARATION, not about
		// non-public constructors as such: a store that states its mechanism explicitly still registers.
		_ = services.AddTenantAwareStore<IProbeStore, PartitionedNonPublicConstructorStore>(
			static _ => PartitionedNonPublicConstructorStore.Create());

		services.ShouldContain(descriptor =>
			descriptor.ServiceType == typeof(ITenantPartitionedCapability<IProbeStore>));
	}

	[Fact]
	public void StillAcceptAStoreWithAPublicAmbientConstructor()
	{
		var services = new ServiceCollection();

		// The ordinary case must keep working, or the refusal above would be indistinguishable from
		// the seam rejecting everything.
		_ = services.AddTenantAwareStore<IProbeStore, PublicAmbientConstructorStore>(
			static _ => new PublicAmbientConstructorStore(new FakeTenantContext()));

		services.ShouldContain(descriptor =>
			descriptor.ServiceType == typeof(ITenantScopingCapability<IProbeStore>));
	}

	[Fact]
	public void RefuseAScopedStoreWhoseConvenienceConstructorOmitsTheTenantContext()
	{
		var services = new ServiceCollection();

		// SAFETY. The store's tenant-aware constructor is what earns the scoped marker, but on this
		// overload the FACTORY chooses which constructor runs — and here it chooses the one that omits
		// the tenant. Resolving ITenantContext ahead of the factory cannot catch that: it proves the host
		// has a tenant context, never that this instance was handed it. Refusing at registration is what
		// makes the marker evidence instead of an assertion.
		var thrown = Should.Throw<InvalidOperationException>(() =>
			services.AddTenantAwareStore<IProbeStore, ConvenienceConstructorStore>(
				static _ => new ConvenienceConstructorStore()));

		// The refusal must name both ways out, or it reports a problem without its fix.
		thrown.Message.ShouldContain(nameof(ITenantContext));
		thrown.Message.ShouldContain("AddTenantAwareStore overload that takes no factory");
	}

	[Fact]
	public void RefuseTheSameShapeOnTheProjectionSeam()
	{
		var services = new ServiceCollection();

		// The projection seam emits the marker for a whole capability FAMILY, so an untenanted store
		// slipping through it attests on behalf of every projection in the host, not just itself.
		_ = Should.Throw<InvalidOperationException>(() =>
			services.AddTenantScopedProjectionStore<IProbeStore, ConvenienceConstructorStore, IProbeStore>(
				static _ => new ConvenienceConstructorStore()));
	}

	[Fact]
	public void StillAcceptTheDelegatingConvenienceShapeOnTheConstructingOverload()
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton<ITenantContext>(new FakeTenantContext());

		// NO-REGRESSION. The same store the factory overload refuses is registered here without
		// complaint, because THIS overload constructs it and passes the tenant context to construction as
		// an explicit argument — so the tenant-blind constructor is unreachable rather than merely
		// unused. The refusal above is about who is holding the constructor, not about the shape of the
		// store; a rule that rejected the delegating pattern outright would reject correct code.
		_ = services.AddTenantAwareStore<IProbeStore, ConvenienceConstructorStore>();

		using var provider = services.BuildServiceProvider();

		provider.GetService<ITenantScopingCapability<IProbeStore>>().ShouldNotBeNull();
		provider.GetRequiredService<ConvenienceConstructorStore>().TenantContext.ShouldNotBeNull(
			"the constructing overload must build through the tenant-aware constructor, or its emitted "
			+ "marker attests something untrue.");
	}

	/// <summary>
	/// The shape the refusal exists for: one constructor reads the ambient tenant, a sibling does not.
	/// </summary>
	private sealed class ConvenienceConstructorStore : IProbeStore
	{
		public ConvenienceConstructorStore()
		{
		}

		public ConvenienceConstructorStore(ITenantContext tenantContext) => TenantContext = tenantContext;

		public ITenantContext? TenantContext { get; }
	}

	private sealed class NonPublicConstructorStore : IProbeStore
	{
		private NonPublicConstructorStore()
		{
		}

		internal static NonPublicConstructorStore Create() => new();
	}

	private sealed class PartitionedNonPublicConstructorStore : IProbeStore, ITenantPartitionedStore
	{
		private PartitionedNonPublicConstructorStore()
		{
		}

		internal static PartitionedNonPublicConstructorStore Create() => new();
	}

	private sealed class PublicAmbientConstructorStore(ITenantContext tenantContext) : IProbeStore
	{
		public ITenantContext TenantContext { get; } = tenantContext;
	}

	private sealed class FakeTenantContext : ITenantContext
	{
		public string? TenantId => "tenant-1";

		public bool HasTenant => true;
	}
}
