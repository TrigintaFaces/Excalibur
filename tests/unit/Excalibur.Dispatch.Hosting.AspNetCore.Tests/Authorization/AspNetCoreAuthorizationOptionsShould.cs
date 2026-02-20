// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AspNetCore;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests.Authorization;

/// <summary>
/// Tests for <see cref="AspNetCoreAuthorizationOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AspNetCoreAuthorizationOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveEnabledTrueByDefault()
	{
		// Act
		var options = new AspNetCoreAuthorizationOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void HaveRequireAuthenticatedUserTrueByDefault()
	{
		// Act
		var options = new AspNetCoreAuthorizationOptions();

		// Assert
		options.RequireAuthenticatedUser.ShouldBeTrue();
	}

	[Fact]
	public void HaveNullDefaultPolicyByDefault()
	{
		// Act
		var options = new AspNetCoreAuthorizationOptions();

		// Assert
		options.DefaultPolicy.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingEnabled()
	{
		// Arrange
		var options = new AspNetCoreAuthorizationOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingRequireAuthenticatedUser()
	{
		// Arrange
		var options = new AspNetCoreAuthorizationOptions();

		// Act
		options.RequireAuthenticatedUser = false;

		// Assert
		options.RequireAuthenticatedUser.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingDefaultPolicy()
	{
		// Arrange
		var options = new AspNetCoreAuthorizationOptions();

		// Act
		options.DefaultPolicy = "AdminOnly";

		// Assert
		options.DefaultPolicy.ShouldBe("AdminOnly");
	}
}
