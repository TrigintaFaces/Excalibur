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
