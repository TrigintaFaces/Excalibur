// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.PolicyData;

using Microsoft.Extensions.Caching.Distributed;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Unit tests for <see cref="AuthorizationPolicyProvider"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class AuthorizationPolicyProviderShould
{
	[Fact]
	public async Task ThrowWhenUserIdIsNull()
	{
		// Arrange
		var currentUser = A.Fake<IAuthenticationToken>();
		A.CallTo(() => currentUser.UserId).Returns(null);

		var tenantId = A.Fake<ITenantId>();
		A.CallTo(() => tenantId.Value).Returns("tenant-1");

		var sut = new AuthorizationPolicyProvider(
			activityGroups: null!,
			userGrants: null!,
			currentUser: currentUser,
			cache: A.Fake<IDistributedCache>(),
			tenantId: tenantId);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(sut.GetPolicyAsync());
		exception.Message.ShouldContain("User ID is required");
	}

	[Fact]
	public async Task ThrowWhenTenantIdIsEmpty()
	{
		// Arrange
		var currentUser = A.Fake<IAuthenticationToken>();
		A.CallTo(() => currentUser.UserId).Returns("user-1");

		var tenantId = A.Fake<ITenantId>();
		A.CallTo(() => tenantId.Value).Returns(string.Empty);

		var sut = new AuthorizationPolicyProvider(
			activityGroups: null!,
			userGrants: null!,
			currentUser: currentUser,
			cache: A.Fake<IDistributedCache>(),
			tenantId: tenantId);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(sut.GetPolicyAsync());
		exception.Message.ShouldContain("Tenant ID is required");
	}

	[Fact]
	public async Task ThrowWhenTenantIdValueIsNull()
	{
		// Arrange
		var currentUser = A.Fake<IAuthenticationToken>();
		A.CallTo(() => currentUser.UserId).Returns("user-1");

		var tenantId = A.Fake<ITenantId>();
		A.CallTo(() => tenantId.Value).Returns(null!);

		var sut = new AuthorizationPolicyProvider(
			activityGroups: null!,
			userGrants: null!,
			currentUser: currentUser,
			cache: A.Fake<IDistributedCache>(),
			tenantId: tenantId);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(sut.GetPolicyAsync());
		exception.Message.ShouldContain("Tenant ID is required");
	}
}
