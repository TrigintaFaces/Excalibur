// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.LeaderElection.Tests;

/// <summary>
/// Regression locks for the opt-in tenant-qualified lease-key helpers
/// (<see cref="TenantLeaderElectionFactoryExtensions"/>).
/// </summary>
/// <remarks>
/// Covers the tenant-aware leader-election acceptance criteria:
/// <list type="bullet">
/// <item><description>A resolved tenant qualifies the lease resource as <c>{resource}:{tenantId}</c>.</description></item>
/// <item><description>Fail-closed: a missing ambient tenant throws <see cref="TenantRequiredException"/> — a missing tenant can NEVER collapse into an unscoped, cross-tenant lease.</description></item>
/// <item><description>The core factory is delegated to with the qualified name (no per-tenant coupling in core LE).</description></item>
/// </list>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
public sealed class TenantLeaderElectionFactoryExtensionsShould
{
	[Fact]
	public void ComposeTenantQualifiedResourceName_WhenTenantResolved()
	{
		var tenant = A.Fake<ITenantContext>();
		A.CallTo(() => tenant.TenantId).Returns("acme");

		tenant.TenantScopedResourceName("orders").ShouldBe("orders:acme");
	}

	[Fact]
	public void ThrowTenantRequired_WhenComposingWithNoTenant()
	{
		var tenant = A.Fake<ITenantContext>();
		A.CallTo(() => tenant.TenantId).Returns(null);

		// Fail-closed: the load-bearing invariant. RED on any impl that returns "orders:" or "orders".
		Should.Throw<TenantRequiredException>(() => tenant.TenantScopedResourceName("orders"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_ForNullOrWhitespaceResource(string? resourceName)
	{
		var tenant = A.Fake<ITenantContext>();
		A.CallTo(() => tenant.TenantId).Returns("acme");

		Should.Throw<ArgumentException>(() => tenant.TenantScopedResourceName(resourceName!));
	}

	[Fact]
	public void CreateElection_WithTenantQualifiedResource_WhenTenantResolved()
	{
		var tenant = A.Fake<ITenantContext>();
		A.CallTo(() => tenant.TenantId).Returns("acme");
		var factory = A.Fake<ILeaderElectionFactory>();
		var election = A.Fake<ILeaderElection>();
		A.CallTo(() => factory.CreateElection("orders:acme", "node-1")).Returns(election);

		var result = factory.CreateTenantScopedElection(tenant, "orders", "node-1");

		result.ShouldBeSameAs(election);
		A.CallTo(() => factory.CreateElection("orders:acme", "node-1")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void FailClosed_CreateElection_WhenNoTenant_AndNeverCallFactory()
	{
		var tenant = A.Fake<ITenantContext>();
		A.CallTo(() => tenant.TenantId).Returns(null);
		var factory = A.Fake<ILeaderElectionFactory>();

		Should.Throw<TenantRequiredException>(() => factory.CreateTenantScopedElection(tenant, "orders"));

		// The core factory must never be reached with an unscoped key.
		A.CallTo(factory).MustNotHaveHappened();
	}

	[Fact]
	public void CreateHealthBasedElection_WithTenantQualifiedResource_WhenTenantResolved()
	{
		var tenant = A.Fake<ITenantContext>();
		A.CallTo(() => tenant.TenantId).Returns("acme");
		var factory = A.Fake<ILeaderElectionFactory>();
		var election = A.Fake<IHealthBasedLeaderElection>();
		A.CallTo(() => factory.CreateHealthBasedElection("orders:acme", null)).Returns(election);

		var result = factory.CreateTenantScopedHealthBasedElection(tenant, "orders");

		result.ShouldBeSameAs(election);
		A.CallTo(() => factory.CreateHealthBasedElection("orders:acme", null)).MustHaveHappenedOnceExactly();
	}
}
