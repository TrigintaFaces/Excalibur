// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Claims;

using Excalibur.A3.Authorization;

using FakeItEasy;

using Microsoft.AspNetCore.Authorization;

using A3IAuthorizationPolicyProvider = Excalibur.A3.Authorization.IAuthorizationPolicyProvider;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Unit tests for <see cref="GrantsAuthorizationHandler"/> public class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class GrantsAuthorizationHandlerShould
{
	private readonly A3IAuthorizationPolicyProvider _policyProvider;
	private readonly GrantsAuthorizationHandler _sut;

	public GrantsAuthorizationHandlerShould()
	{
		_policyProvider = A.Fake<A3IAuthorizationPolicyProvider>();
		_sut = new GrantsAuthorizationHandler(_policyProvider);
	}

	[Fact]
	public void ImplementAuthorizationHandler()
	{
		// Assert
		_sut.ShouldBeAssignableTo<IAuthorizationHandler>();
	}

	[Fact]
	public void BePublicAndSealed()
	{
		// Assert
		typeof(GrantsAuthorizationHandler).IsPublic.ShouldBeTrue();
		typeof(GrantsAuthorizationHandler).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public async Task SucceedWhenUserIsAuthorized()
	{
		// Arrange
		var requirement = new GrantsAuthorizationRequirement("TestActivity", ["Resource"]);
		var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user1")], "test"));
		var context = new AuthorizationHandlerContext([requirement], user, null);

		var policy = A.Fake<IAuthorizationPolicy>();
		A.CallTo(() => policy.IsAuthorized("TestActivity", null)).Returns(true);
		A.CallTo(() => _policyProvider.GetPolicyAsync()).Returns(Task.FromResult(policy));

		// Act
		await _sut.HandleAsync(context);

		// Assert
		context.HasSucceeded.ShouldBeTrue();
	}

	[Fact]
	public async Task FailWhenUserIsNotAuthorized()
	{
		// Arrange
		var requirement = new GrantsAuthorizationRequirement("TestActivity", ["Resource"]);
		var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user1")], "test"));
		var context = new AuthorizationHandlerContext([requirement], user, null);

		var policy = A.Fake<IAuthorizationPolicy>();
		A.CallTo(() => policy.IsAuthorized("TestActivity", null)).Returns(false);
		A.CallTo(() => _policyProvider.GetPolicyAsync()).Returns(Task.FromResult(policy));

		// Act
		await _sut.HandleAsync(context);

		// Assert
		context.HasFailed.ShouldBeTrue();
	}

	[Fact]
	public async Task PassResourceIdToPolicy()
	{
		// Arrange
		var requirement = new GrantsAuthorizationRequirement("TestActivity", ["Resource"], "resource-123");
		var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user1")], "test"));
		var context = new AuthorizationHandlerContext([requirement], user, null);

		var policy = A.Fake<IAuthorizationPolicy>();
		A.CallTo(() => policy.IsAuthorized("TestActivity", "resource-123")).Returns(true);
		A.CallTo(() => _policyProvider.GetPolicyAsync()).Returns(Task.FromResult(policy));

		// Act
		await _sut.HandleAsync(context);

		// Assert
		A.CallTo(() => policy.IsAuthorized("TestActivity", "resource-123")).MustHaveHappenedOnceExactly();
	}

	// nucnze regression lock: a missing principal/tenant makes GetPolicyAsync throw InvalidOperationException
	// (its documented contract). The handler must DENY CLEANLY (context.Fail() -> 403), NOT let the exception
	// escape as an unhandled 500. Fail-closed posture is preserved either way; this pins the clean status code.
	// RED on the pre-fix handler, which awaited GetPolicyAsync() with no catch and rethrew.
	[Fact]
	public async Task FailCleanly_WhenPolicyProviderThrowsForMissingPrincipalOrTenant()
	{
		// Arrange
		var requirement = new GrantsAuthorizationRequirement("TestActivity", ["Resource"]);
		var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user1")], "test"));
		var context = new AuthorizationHandlerContext([requirement], user, null);

		A.CallTo(() => _policyProvider.GetPolicyAsync())
			.Throws(new InvalidOperationException("Tenant ID is required for authorization policy."));

		// Act — must NOT throw out of the handler (a throw here surfaces as HTTP 500).
		await Should.NotThrowAsync(async () => await _sut.HandleAsync(context));

		// Assert — clean authorization failure (fail-closed 403), not a server fault.
		context.HasFailed.ShouldBeTrue("a missing principal/tenant must deny cleanly (context.Fail), not throw");
		context.HasSucceeded.ShouldBeFalse();
	}

	[Fact]
	public async Task CallPolicyProviderOnce()
	{
		// Arrange
		var requirement = new GrantsAuthorizationRequirement("TestActivity", ["Resource"]);
		var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user1")], "test"));
		var context = new AuthorizationHandlerContext([requirement], user, null);

		var policy = A.Fake<IAuthorizationPolicy>();
		A.CallTo(() => policy.IsAuthorized(A<string>.Ignored, A<string?>.Ignored)).Returns(true);
		A.CallTo(() => _policyProvider.GetPolicyAsync()).Returns(Task.FromResult(policy));

		// Act
		await _sut.HandleAsync(context);

		// Assert
		A.CallTo(() => _policyProvider.GetPolicyAsync()).MustHaveHappenedOnceExactly();
	}
}
