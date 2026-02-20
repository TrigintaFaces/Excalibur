// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Options.Middleware;

namespace Excalibur.Dispatch.Tests.Options.Middleware;

/// <summary>
/// Unit tests for <see cref="TenantIdentityOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class TenantIdentityOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsTrue()
	{
		// Arrange & Act
		var options = new TenantIdentityOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Default_ValidateTenantAccess_IsTrue()
	{
		// Arrange & Act
		var options = new TenantIdentityOptions();

		// Assert
		options.ValidateTenantAccess.ShouldBeTrue();
	}

	[Fact]
	public void Default_TenantIdHeader_IsXTenantId()
	{
		// Arrange & Act
		var options = new TenantIdentityOptions();

		// Assert
		options.TenantIdHeader.ShouldBe("X-Tenant-ID");
	}

	[Fact]
	public void Default_TenantNameHeader_IsXTenantName()
	{
		// Arrange & Act
		var options = new TenantIdentityOptions();

		// Assert
		options.TenantNameHeader.ShouldBe("X-Tenant-Name");
	}

	[Fact]
	public void Default_TenantRegionHeader_IsXTenantRegion()
	{
		// Arrange & Act
		var options = new TenantIdentityOptions();

		// Assert
		options.TenantRegionHeader.ShouldBe("X-Tenant-Region");
	}

	[Fact]
	public void Default_DefaultTenantId_IsTenantDefaultsValue()
	{
		// Arrange & Act
		var options = new TenantIdentityOptions();

		// Assert
		options.DefaultTenantId.ShouldBe(TenantDefaults.DefaultTenantId);
	}

	[Fact]
	public void Default_MinTenantIdLength_IsOne()
	{
		// Arrange & Act
		var options = new TenantIdentityOptions();

		// Assert
		options.MinTenantIdLength.ShouldBe(1);
	}

	[Fact]
	public void Default_MaxTenantIdLength_IsOneHundred()
	{
		// Arrange & Act
		var options = new TenantIdentityOptions();

		// Assert
		options.MaxTenantIdLength.ShouldBe(100);
	}

	[Fact]
	public void Default_TenantIdPattern_IsNull()
	{
		// Arrange & Act
		var options = new TenantIdentityOptions();

		// Assert
		options.TenantIdPattern.ShouldBeNull();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new TenantIdentityOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void ValidateTenantAccess_CanBeSet()
	{
		// Arrange
		var options = new TenantIdentityOptions();

		// Act
		options.ValidateTenantAccess = false;

		// Assert
		options.ValidateTenantAccess.ShouldBeFalse();
	}

	[Fact]
	public void TenantIdHeader_CanBeSet()
	{
		// Arrange
		var options = new TenantIdentityOptions();

		// Act
		options.TenantIdHeader = "Custom-Tenant-Header";

		// Assert
		options.TenantIdHeader.ShouldBe("Custom-Tenant-Header");
	}

	[Fact]
	public void DefaultTenantId_CanBeSet()
	{
		// Arrange
		var options = new TenantIdentityOptions();

		// Act
		options.DefaultTenantId = "default-tenant";

		// Assert
		options.DefaultTenantId.ShouldBe("default-tenant");
	}

	[Fact]
	public void MinTenantIdLength_CanBeSet()
	{
		// Arrange
		var options = new TenantIdentityOptions();

		// Act
		options.MinTenantIdLength = 5;

		// Assert
		options.MinTenantIdLength.ShouldBe(5);
	}

	[Fact]
	public void MaxTenantIdLength_CanBeSet()
	{
		// Arrange
		var options = new TenantIdentityOptions();

		// Act
		options.MaxTenantIdLength = 50;

		// Assert
		options.MaxTenantIdLength.ShouldBe(50);
	}

	[Fact]
	public void TenantIdPattern_CanBeSet()
	{
		// Arrange
		var options = new TenantIdentityOptions();

		// Act
		options.TenantIdPattern = @"^[a-z0-9\-]+$";

		// Assert
		options.TenantIdPattern.ShouldBe(@"^[a-z0-9\-]+$");
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new TenantIdentityOptions
		{
			Enabled = false,
			ValidateTenantAccess = false,
			TenantIdHeader = "Custom-Header",
			TenantNameHeader = "Custom-Name",
			TenantRegionHeader = "Custom-Region",
			DefaultTenantId = "default",
			MinTenantIdLength = 3,
			MaxTenantIdLength = 50,
			TenantIdPattern = @"^\w+$",
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.ValidateTenantAccess.ShouldBeFalse();
		options.TenantIdHeader.ShouldBe("Custom-Header");
		options.TenantNameHeader.ShouldBe("Custom-Name");
		options.TenantRegionHeader.ShouldBe("Custom-Region");
		options.DefaultTenantId.ShouldBe("default");
		options.MinTenantIdLength.ShouldBe(3);
		options.MaxTenantIdLength.ShouldBe(50);
		options.TenantIdPattern.ShouldBe(@"^\w+$");
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForMultiTenantApp_HasValidation()
	{
		// Act
		var options = new TenantIdentityOptions
		{
			Enabled = true,
			ValidateTenantAccess = true,
			MinTenantIdLength = 3,
			MaxTenantIdLength = 36,
			TenantIdPattern = @"^[a-f0-9\-]{36}$", // UUID pattern
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.ValidateTenantAccess.ShouldBeTrue();
		_ = options.TenantIdPattern.ShouldNotBeNull();
	}

	[Fact]
	public void Options_ForSingleTenant_HasDefaultTenantId()
	{
		// Act
		var options = new TenantIdentityOptions
		{
			Enabled = true,
			DefaultTenantId = "primary",
			ValidateTenantAccess = false,
		};

		// Assert
		_ = options.DefaultTenantId.ShouldNotBeNull();
		options.ValidateTenantAccess.ShouldBeFalse();
	}

	#endregion
}
