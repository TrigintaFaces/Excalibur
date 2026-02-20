// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using TenantId = Excalibur.Dispatch.Delivery.TenantId;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

/// <summary>
/// Unit tests for <see cref="TenantId"/>.
/// </summary>
/// <remarks>
/// Tests the tenant identifier implementation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Delivery")]
[Trait("Priority", "0")]
public sealed class TenantIdShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_Default_InitializesWithEmptyValue()
	{
		// Arrange & Act
		var tenantId = new TenantId();

		// Assert
		_ = tenantId.ShouldNotBeNull();
		tenantId.Value.ShouldBe(string.Empty);
	}

	#endregion

	#region Value Property Tests

	[Fact]
	public void Value_CanBeSet()
	{
		// Arrange
		var tenantId = new TenantId();

		// Act
		tenantId.Value = "tenant-123";

		// Assert
		tenantId.Value.ShouldBe("tenant-123");
	}

	[Theory]
	[InlineData("tenant-1")]
	[InlineData("acme-corp")]
	[InlineData("00000000-0000-0000-0000-000000000001")]
	[InlineData("prod-us-east-tenant")]
	[InlineData("")]
	public void Value_WithVariousTenantIds_Works(string value)
	{
		// Arrange
		var tenantId = new TenantId();

		// Act
		tenantId.Value = value;

		// Assert
		tenantId.Value.ShouldBe(value);
	}

	[Fact]
	public void Value_CanBeChangedMultipleTimes()
	{
		// Arrange
		var tenantId = new TenantId();

		// Act
		tenantId.Value = "first";
		tenantId.Value = "second";
		tenantId.Value = "third";

		// Assert
		tenantId.Value.ShouldBe("third");
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsValue()
	{
		// Arrange
		var tenantId = new TenantId { Value = "tenant-xyz" };

		// Act
		var result = tenantId.ToString();

		// Assert
		result.ShouldBe("tenant-xyz");
	}

	[Fact]
	public void ToString_WithEmptyValue_ReturnsEmptyString()
	{
		// Arrange
		var tenantId = new TenantId();

		// Act
		var result = tenantId.ToString();

		// Assert
		result.ShouldBe(string.Empty);
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsITenantId()
	{
		// Arrange & Act
		var tenantId = new TenantId();

		// Assert
		_ = tenantId.ShouldBeAssignableTo<ITenantId>();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsValue()
	{
		// Arrange & Act
		var tenantId = new TenantId
		{
			Value = "initialized-tenant",
		};

		// Assert
		tenantId.Value.ShouldBe("initialized-tenant");
	}

	#endregion

	#region Typical Usage Scenarios

	[Fact]
	public void MultiTenantScenario_TenantIsolation()
	{
		// Arrange - Two different tenants
		var tenant1 = new TenantId { Value = "acme-corp" };
		var tenant2 = new TenantId { Value = "contoso-inc" };

		// Assert - They should be different
		tenant1.Value.ShouldNotBe(tenant2.Value);
	}

	[Fact]
	public void GuidBasedTenantId_Scenario()
	{
		// Arrange & Act
		var guid = Guid.NewGuid();
		var tenantId = new TenantId { Value = guid.ToString() };

		// Assert
		Guid.TryParse(tenantId.Value, out var parsedGuid).ShouldBeTrue();
		parsedGuid.ShouldBe(guid);
	}

	[Fact]
	public void HierarchicalTenantId_Scenario()
	{
		// Arrange & Act - Hierarchical tenant structure (org > department > team)
		var tenantId = new TenantId { Value = "acme-corp.engineering.platform" };

		// Assert
		tenantId.Value.ShouldContain("acme-corp");
		tenantId.Value.ShouldContain("engineering");
		tenantId.Value.ShouldContain("platform");
	}

	#endregion
}
