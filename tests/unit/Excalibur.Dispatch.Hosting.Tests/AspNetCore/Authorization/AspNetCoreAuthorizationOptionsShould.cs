// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AspNetCore;

namespace Excalibur.Dispatch.Hosting.Tests.AspNetCore.Authorization;

/// <summary>
/// Unit tests for <see cref="AspNetCoreAuthorizationOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
public sealed class AspNetCoreAuthorizationOptionsShould
{
	[Fact]
	public void HaveEnabledTrueByDefault()
	{
		// Arrange & Act
		var options = new AspNetCoreAuthorizationOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void HaveRequireAuthenticatedUserTrueByDefault()
	{
		// Arrange & Act
		var options = new AspNetCoreAuthorizationOptions();

		// Assert
		options.RequireAuthenticatedUser.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultPolicyNullByDefault()
	{
		// Arrange & Act
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
		options.DefaultPolicy = "MyPolicy";

		// Assert
		options.DefaultPolicy.ShouldBe("MyPolicy");
	}

	[Fact]
	public void AllowSettingDefaultPolicyToNull()
	{
		// Arrange
		var options = new AspNetCoreAuthorizationOptions { DefaultPolicy = "InitialPolicy" };

		// Act
		options.DefaultPolicy = null;

		// Assert
		options.DefaultPolicy.ShouldBeNull();
	}

	[Fact]
	public void AllowObjectInitializerSyntax()
	{
		// Arrange & Act
		var options = new AspNetCoreAuthorizationOptions
		{
			Enabled = false,
			RequireAuthenticatedUser = false,
			DefaultPolicy = "CustomPolicy"
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.RequireAuthenticatedUser.ShouldBeFalse();
		options.DefaultPolicy.ShouldBe("CustomPolicy");
	}
}
