// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

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

	/// <summary>
	/// The middleware composes a bare <c>[Authorize]</c> through the host's own policy provider, so the
	/// host's <c>AuthorizationOptions.DefaultPolicy</c> is the single place a default policy is configured.
	/// A second knob here would be a second place for the two to disagree, and the one that silently won
	/// was the weaker of the pair. This arm goes RED if one is ever re-added.
	/// </summary>
	[Fact]
	public void ExposeExactlyTheTwoSupportedOptions()
	{
		// Arrange & Act
		var propertyNames = typeof(AspNetCoreAuthorizationOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Select(p => p.Name)
			.ToArray();

		// Assert
		propertyNames.ShouldBe(["Enabled", "RequireAuthenticatedUser"], ignoreOrder: true);
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
	public void SetEnabled_WithoutDisturbingRequireAuthenticatedUser()
	{
		// Arrange
		var options = new AspNetCoreAuthorizationOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.RequireAuthenticatedUser.ShouldBeTrue();
	}

	[Fact]
	public void SetRequireAuthenticatedUser_WithoutDisturbingEnabled()
	{
		// Arrange
		var options = new AspNetCoreAuthorizationOptions();

		// Act
		options.RequireAuthenticatedUser = false;

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void AllowObjectInitializerSyntax()
	{
		// Arrange & Act
		var options = new AspNetCoreAuthorizationOptions
		{
			Enabled = false,
			RequireAuthenticatedUser = false
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.RequireAuthenticatedUser.ShouldBeFalse();
	}
}
