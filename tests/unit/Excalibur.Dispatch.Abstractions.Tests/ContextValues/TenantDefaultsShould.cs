// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Tests.ContextValues;

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
		TenantDefaults.DefaultTenantId.ShouldBe("__default__");
	}

	[Fact]
	public void Constants_Should_NotBeNullOrEmpty()
	{
		// Assert
		TenantDefaults.DefaultTenantId.ShouldNotBeNullOrEmpty();
	}
}
