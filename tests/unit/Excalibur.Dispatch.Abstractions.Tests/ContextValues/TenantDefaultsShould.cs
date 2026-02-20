// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests.ContextValues;

/// <summary>
/// Unit tests for the <see cref="TenantDefaults"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class TenantDefaultsShould
{
	[Fact]
	public void DefaultTenantId_Should_BeDefault()
	{
		// Assert
		TenantDefaults.DefaultTenantId.ShouldBe("Default");
	}

	[Fact]
	public void AllTenants_Should_BeWildcard()
	{
		// Assert
		TenantDefaults.AllTenants.ShouldBe("*");
	}

	[Fact]
	public void Constants_Should_NotBeNullOrEmpty()
	{
		// Assert
		TenantDefaults.DefaultTenantId.ShouldNotBeNullOrEmpty();
		TenantDefaults.AllTenants.ShouldNotBeNullOrEmpty();
	}
}
