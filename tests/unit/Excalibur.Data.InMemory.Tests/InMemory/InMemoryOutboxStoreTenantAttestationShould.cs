// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Outbox.InMemory;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Binds both shipped in-memory outbox registration entry points to the tenant-aware seam, so each attests
/// the tenant mechanism the store actually implements.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="InMemoryOutboxStore"/> is tenant-partitioned, not tenant-scoped: its constructor takes no
/// <see cref="ITenantContext"/>, and it persists <c>TenantId</c> on every message and hands it back
/// unchanged on drain -- the same mechanism the SqlServer/Postgres/Oracle outbox stores attest. Before this
/// fix neither registration entry point emitted <see cref="ITenantPartitionedCapability{TContract}"/>, so a
/// multi-tenant host registering the in-memory outbox -- the framework's own default for tests and local
/// development -- was refused at startup by <c>RequireTenantPartitionedCapability&lt;IOutboxStore&gt;</c>
/// even though the store already scoped correctly.
/// </para>
/// <para>
/// Both entry points are covered because they are two independent registration paths
/// (<c>AddInMemoryOutboxStore</c> and the builder-driven <c>UseInMemory()</c>), and a fix to one alone
/// would leave the other silently unattested.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "DataInMemory")]
[Trait("Priority", "1")]
public sealed class InMemoryOutboxStoreTenantAttestationShould : UnitTestBase
{
	/// <summary>
	/// SAFETY: the low-level <c>AddInMemoryOutboxStore</c> entry point attests the tenant-partitioned
	/// mechanism it implements.
	/// </summary>
	[Fact]
	public void AttestPartitionedCapability_ForAddInMemoryOutboxStore()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddInMemoryOutboxStore();

		using var provider = services.BuildServiceProvider();

		provider.GetService<ITenantPartitionedCapability<IOutboxStore>>().ShouldNotBeNull(
			"InMemoryOutboxStore persists TenantId per message and re-establishes it on drain, so it must "
			+ "attest the row-partitioned mechanism through the seam that wires it; a multi-tenant host "
			+ "otherwise refuses the framework's own default outbox store at startup.");
	}

	/// <summary>
	/// LIVENESS: attesting did not cost the registration it describes.
	/// </summary>
	[Fact]
	public void StillResolveTheStoreItAdvertises_ForAddInMemoryOutboxStore()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddInMemoryOutboxStore();

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredKeyedService<IOutboxStore>("default").ShouldNotBeNull();
	}

	/// <summary>
	/// SAFETY: the builder-driven <c>UseInMemory()</c> entry point attests the same mechanism, independently
	/// of the low-level entry point above.
	/// </summary>
	[Fact]
	public void AttestPartitionedCapability_ForUseInMemory()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburOutbox(outbox => outbox.UseInMemory());

		using var provider = services.BuildServiceProvider();

		provider.GetService<ITenantPartitionedCapability<IOutboxStore>>().ShouldNotBeNull(
			"UseInMemory() is a second, independent registration path for the same store and must attest "
			+ "the same mechanism -- a fix applied only to AddInMemoryOutboxStore would leave this path "
			+ "unattested.");
	}

	/// <summary>
	/// LIVENESS: attesting did not cost the registration it describes, on the builder-driven path.
	/// </summary>
	[Fact]
	public void StillResolveTheStoreItAdvertises_ForUseInMemory()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburOutbox(outbox => outbox.UseInMemory());

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredKeyedService<IOutboxStore>("default").ShouldNotBeNull();
	}
}
