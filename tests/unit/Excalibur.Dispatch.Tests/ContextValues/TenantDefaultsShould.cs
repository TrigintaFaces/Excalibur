// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.ContextValues;

/// <summary>
/// Unit tests for <see cref="TenantDefaults"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class TenantDefaultsShould
{
	[Fact]
	public void HaveDefaultTenantIdEqualToDefault()
	{
		// Assert
		TenantDefaults.DefaultTenantId.ShouldBe("Default");
	}

	[Fact]
	public void HaveNonNullOrEmptyDefaultTenantId()
	{
		// Assert
		TenantDefaults.DefaultTenantId.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void HaveAllTenantsEqualToWildcard()
	{
		// Assert
		TenantDefaults.AllTenants.ShouldBe("*");
	}

	[Fact]
	public void HaveDistinctDefaultAndAllTenantValues()
	{
		// Assert — the default single-tenant value and wildcard must differ
		TenantDefaults.DefaultTenantId.ShouldNotBe(TenantDefaults.AllTenants);
	}
}
