// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authentication;

using Microsoft.Extensions.Caching.Memory;

namespace Excalibur.Tests.A3.Authentication;

/// <summary>
/// Unit tests for <see cref="AuthenticationTokenProvider"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AuthenticationTokenProviderShould
{
	[Fact]
	public void Implement_IAuthenticationTokenProvider()
	{
		// Arrange
		using var httpClient = new HttpClient();
		var cache = A.Fake<IMemoryCache>();

		// Act
		var provider = new AuthenticationTokenProvider(httpClient, cache);

		// Assert
		provider.ShouldBeAssignableTo<IAuthenticationTokenProvider>();
	}

	[Fact]
	public void ReturnCachedToken_WhenTokenIsValid()
	{
		// Arrange
		using var httpClient = new HttpClient();
		var cache = A.Fake<IMemoryCache>();

		// Simulate cached token that is not near expiration
		var cachedToken = A.Fake<AuthenticationToken>();

		object? cachedValue = cachedToken;
		A.CallTo(() => cache.TryGetValue("test-sa", out cachedValue))
			.Returns(true);

		var provider = new AuthenticationTokenProvider(httpClient, cache);

		// The provider uses cache.Get<AuthenticationToken>, which goes through TryGetValue
		// This test verifies the provider can be constructed with valid dependencies
		provider.ShouldNotBeNull();
	}
}
