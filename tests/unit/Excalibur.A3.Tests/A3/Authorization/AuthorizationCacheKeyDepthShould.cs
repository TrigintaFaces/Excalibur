// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Excalibur.Domain;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Depth unit tests for <see cref="AuthorizationCacheKey"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Collection("ApplicationContext")]
public sealed class AuthorizationCacheKeyDepthShould : IDisposable
{
	public AuthorizationCacheKeyDepthShould() => ApplicationContext.Reset();

	public void Dispose() => ApplicationContext.Reset();

	[Fact]
	public void ForGrants_ThrowsOnNullUserId() =>
		Should.Throw<ArgumentException>(() => AuthorizationCacheKey.ForGrants(null!));

	[Fact]
	public void ForGrants_ThrowsOnEmptyUserId() =>
		Should.Throw<ArgumentException>(() => AuthorizationCacheKey.ForGrants(""));

	[Fact]
	public void ForGrants_ThrowsOnWhitespaceUserId() =>
		Should.Throw<ArgumentException>(() => AuthorizationCacheKey.ForGrants("   "));

	[Fact]
	public void ForGrants_SucceedsWithNoApplicationContext()
	{
		// Was ForGrants_ThrowsWhenBasePathNotConfigured. The context is reset in the constructor AND
		// again here, so there is provably no ambient configuration to fall back on -- and the key is
		// still produced. That inversion is the fix: a consumer who configured nothing beyond the
		// documented surface can now read and invalidate grants instead of receiving
		// InvalidConfigurationException from a key builder.
		ApplicationContext.Reset();

		AuthorizationCacheKey.ForGrants("user-1").ShouldBe("authorization/user-1/grants");
	}

	[Fact]
	public void ForActivityGroups_SucceedsWithNoApplicationContext()
	{
		// Was ForActivityGroups_ThrowsWhenBasePathNotConfigured. Same inversion.
		ApplicationContext.Reset();

		AuthorizationCacheKey.ForActivityGroups("tenant-1").ShouldBe("authorization/tenant-1/activity-groups/v3");
	}
}
