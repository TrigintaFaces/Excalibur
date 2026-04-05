// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization;

namespace Excalibur.A3.Tests.A3.Authorization;

/// <summary>
/// Unit tests for <see cref="AuthorizationPolicyExtensions"/> convenience overloads.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class AuthorizationPolicyExtensionsShould
{
	[Fact]
	public void IsAuthorized_WithoutResourceId_DelegatesToFullMethod()
	{
		// Arrange
		var policy = A.Fake<IAuthorizationPolicy>();
		A.CallTo(() => policy.IsAuthorized("ViewOrders", null)).Returns(true);

		// Act
		var result = policy.IsAuthorized("ViewOrders");

		// Assert
		result.ShouldBeTrue();
		A.CallTo(() => policy.IsAuthorized("ViewOrders", null))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void IsAuthorized_WithoutResourceId_ReturnsFalseWhenNotAuthorized()
	{
		// Arrange
		var policy = A.Fake<IAuthorizationPolicy>();
		A.CallTo(() => policy.IsAuthorized("DeleteUsers", null)).Returns(false);

		// Act
		var result = policy.IsAuthorized("DeleteUsers");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsAuthorized_WithoutResourceId_PassesNullResourceId()
	{
		// Arrange
		var policy = A.Fake<IAuthorizationPolicy>();

		// Act
		policy.IsAuthorized("ManageSettings");

		// Assert
		A.CallTo(() => policy.IsAuthorized("ManageSettings", null))
			.MustHaveHappenedOnceExactly();
	}
}
