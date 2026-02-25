// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization;
using Excalibur.Domain;
using Excalibur.Domain.Exceptions;

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
	public void ForGrants_ThrowsOnNullUserId()
	{
		Should.Throw<ArgumentException>(() =>
			AuthorizationCacheKey.ForGrants(null!));
	}

	[Fact]
	public void ForGrants_ThrowsOnEmptyUserId()
	{
		Should.Throw<ArgumentException>(() =>
			AuthorizationCacheKey.ForGrants(""));
	}

	[Fact]
	public void ForGrants_ThrowsOnWhitespaceUserId()
	{
		Should.Throw<ArgumentException>(() =>
			AuthorizationCacheKey.ForGrants("   "));
	}

	[Fact]
	public void ForGrants_ThrowsWhenBasePathNotConfigured()
	{
		ApplicationContext.Reset();

		Should.Throw<InvalidConfigurationException>(() =>
			AuthorizationCacheKey.ForGrants("user-1"));
	}

	[Fact]
	public void ForActivityGroups_ThrowsWhenBasePathNotConfigured()
	{
		ApplicationContext.Reset();

		Should.Throw<InvalidConfigurationException>(() =>
			AuthorizationCacheKey.ForActivityGroups());
	}
}
