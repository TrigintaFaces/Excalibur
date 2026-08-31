// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Tests.ContextValues;

/// <summary>
/// Unit tests for <see cref="TenantDefaults"/>.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch")]
public sealed class TenantDefaultsShould
{
	[Fact]
	public void HaveDefaultTenantIdEqualToDefault()
	{
		// Assert
		TenantDefaults.DefaultTenantId.ShouldBe("__default__");
	}

	[Fact]
	public void HaveNonNullOrEmptyDefaultTenantId()
	{
		// Assert
		TenantDefaults.DefaultTenantId.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void NotOfferAWildcardTenantIdentifier()
	{
		// The wildcard "all tenants" constant was removed, not renamed. Its documentation described a
		// comparison against stored tenant terms that was never implemented anywhere, so every row written
		// under it landed in a partition no scoped read could return, while a reader of that documentation
		// believed a wildcard comparison was protecting them.
		//
		// This asserts the ABSENCE structurally rather than by grep: estate-wide operations are reached by
		// NAME (PurgeAllTenantsCompletedBeforeAsync, CleanupAllTenantsSentMessagesAsync), and re-introducing
		// a magic tenant value would give callers a second, unenforced route to the same intent.
		typeof(TenantDefaults)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Select(field => field.Name)
			.ShouldNotContain(
				"AllTenants",
				"a wildcard tenant identifier is not a mechanism — nothing compares against it, so a row "
				+ "carrying it is addressable by no scoped read");
	}
}
