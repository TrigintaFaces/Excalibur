// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

/// <summary>
/// Unit tests for <see cref="TenantId"/>.
/// </summary>
/// <remarks>
/// Tests the tenant identifier implementation.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Delivery")]
[Trait("Priority", "0")]
public sealed class TenantIdShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_RejectsAMissingValueRatherThanCoercingIt()
	{
		// Was: a default-constructed identifier holds "". An empty tenant is not a tenant — downstream it
		// matches nothing, or everything where absence reads as unscoped. Rejection happens at the point
		// the mistake is made, which is the only place it can still be attributed to a caller.
		_ = Should.Throw<ArgumentNullException>(() => new TenantId(null!));
		_ = Should.Throw<ArgumentException>(() => new TenantId(string.Empty));
		_ = Should.Throw<ArgumentException>(() => new TenantId("   "));
	}

	#endregion

	#region Value Property Tests

	[Fact]
	public void Value_IsFixedAtConstruction()
	{
		// Was: Value_CanBeSet. A mutable tenant identifier can change after every scope check that read it,
		// so the value that authorised an operation need not be the value it runs under. There is no setter
		// now; the property is asserted by construction instead.
		var tenantId = new TenantId("tenant-123");

		// Assert
		tenantId.Value.ShouldBe("tenant-123");
	}

	[Theory]
	[InlineData("tenant-1")]
	[InlineData("acme-corp")]
	[InlineData("00000000-0000-0000-0000-000000000001")]
	[InlineData("prod-us-east-tenant")]
	public void Value_WithVariousTenantIds_Works(string value)
	{
		var tenantId = new TenantId(value);

		tenantId.Value.ShouldBe(value);
	}

	/// <remarks>
	/// The empty string used to be the FIFTH case of the theory above, asserting it round-tripped like any
	/// other identifier. It is moved here and inverted rather than dropped, because "" is the one input
	/// whose old behaviour was the defect: it produced an identifier that names no tenant and reads as
	/// valid everywhere downstream.
	/// </remarks>
	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("\t")]
	public void Value_WithAMissingTenantId_IsRejected(string value)
	{
		_ = Should.Throw<ArgumentException>(() => new TenantId(value));
	}

	[Fact]
	public void DistinctValues_RequireDistinctInstances()
	{
		// Was: Value_CanBeChangedMultipleTimes, which reassigned one instance through three tenants. That
		// is precisely the hazard — one object that was three different tenants over its lifetime, each
		// after whatever check had already read it. Changing tenant now means constructing a new
		// identifier, so a reference captured earlier still names who it named.
		var first = new TenantId("first");
		var third = new TenantId("third");

		first.Value.ShouldBe("first");
		third.Value.ShouldBe("third");
		first.Equals(third).ShouldBeFalse();
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsValue()
	{
		// Arrange
		var tenantId = new TenantId("tenant-xyz");

		// Act
		var result = tenantId.ToString();

		// Assert
		result.ShouldBe("tenant-xyz");
	}

	[Fact]
	public void ToString_NeverReturnsEmpty_BecauseAnEmptyIdentifierCannotExist()
	{
		// Arrange -- an empty identifier can no longer be constructed, so the invariant replaces the old
		// assertion that ToString rendered one as "".
		var tenantId = new TenantId("t");

		// Act
		var result = tenantId.ToString();

		// Assert
		result.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion


	#region Construction Tests

	[Fact]
	public void Construction_RequiresTheValueUpFront()
	{
		// This was an object-initializer test: `new TenantId { Value = "..." }`. That shape needs a
		// parameterless constructor and a settable property, which together mean an identifier exists in an
		// empty state before it is populated -- and stays mutable afterwards. Both are gone. The value is
		// now required at construction, so there is no window in which a TenantId names no tenant.
		var tenantId = new TenantId("initialized-tenant");

		tenantId.Value.ShouldBe("initialized-tenant");
	}

	#endregion

	#region Typical Usage Scenarios

	[Fact]
	public void MultiTenantScenario_TenantIsolation()
	{
		// Arrange - Two different tenants
		var tenant1 = new TenantId("acme-corp");
		var tenant2 = new TenantId("contoso-inc");

		// Assert - They should be different
		tenant1.Value.ShouldNotBe(tenant2.Value);
	}

	[Fact]
	public void GuidBasedTenantId_Scenario()
	{
		// Arrange & Act
		var guid = Guid.NewGuid();
		var tenantId = new TenantId(guid.ToString());

		// Assert
		Guid.TryParse(tenantId.Value, out var parsedGuid).ShouldBeTrue();
		parsedGuid.ShouldBe(guid);
	}

	[Fact]
	public void HierarchicalTenantId_Scenario()
	{
		// Arrange & Act - Hierarchical tenant structure (org > department > team)
		var tenantId = new TenantId("acme-corp.engineering.platform");

		// Assert
		tenantId.Value.ShouldContain("acme-corp");
		tenantId.Value.ShouldContain("engineering");
		tenantId.Value.ShouldContain("platform");
	}

	#endregion
}
