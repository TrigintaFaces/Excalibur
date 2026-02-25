// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Middleware;

namespace Excalibur.Dispatch.Tests.Options.Middleware;

/// <summary>
/// Unit tests for <see cref="AuthorizationOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class AuthorizationOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsTrue()
	{
		// Arrange & Act
		var options = new AuthorizationOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Default_AllowAnonymousAccess_IsFalse()
	{
		// Arrange & Act
		var options = new AuthorizationOptions();

		// Assert
		options.AllowAnonymousAccess.ShouldBeFalse();
	}

	[Fact]
	public void Default_BypassAuthorizationForTypes_IsNull()
	{
		// Arrange & Act
		var options = new AuthorizationOptions();

		// Assert
		options.BypassAuthorizationForTypes.ShouldBeNull();
	}

	[Fact]
	public void Default_DefaultPolicyName_IsDefault()
	{
		// Arrange & Act
		var options = new AuthorizationOptions();

		// Assert
		options.DefaultPolicyName.ShouldBe("Default");
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new AuthorizationOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowAnonymousAccess_CanBeSet()
	{
		// Arrange
		var options = new AuthorizationOptions();

		// Act
		options.AllowAnonymousAccess = true;

		// Assert
		options.AllowAnonymousAccess.ShouldBeTrue();
	}

	[Fact]
	public void BypassAuthorizationForTypes_CanBeSet()
	{
		// Arrange
		var options = new AuthorizationOptions();
		var types = new[] { "HealthCheck", "PublicQuery" };

		// Act
		options.BypassAuthorizationForTypes = types;

		// Assert
		options.BypassAuthorizationForTypes.ShouldBe(types);
	}

	[Fact]
	public void DefaultPolicyName_CanBeSet()
	{
		// Arrange
		var options = new AuthorizationOptions();

		// Act
		options.DefaultPolicyName = "AdminOnly";

		// Assert
		options.DefaultPolicyName.ShouldBe("AdminOnly");
	}

	[Fact]
	public void DefaultPolicyName_CanBeSetToNull()
	{
		// Arrange
		var options = new AuthorizationOptions();

		// Act
		options.DefaultPolicyName = null;

		// Assert
		options.DefaultPolicyName.ShouldBeNull();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Arrange
		var bypassTypes = new[] { "PublicEndpoint" };

		// Act
		var options = new AuthorizationOptions
		{
			Enabled = false,
			AllowAnonymousAccess = true,
			BypassAuthorizationForTypes = bypassTypes,
			DefaultPolicyName = "CustomPolicy",
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.AllowAnonymousAccess.ShouldBeTrue();
		options.BypassAuthorizationForTypes.ShouldBe(bypassTypes);
		options.DefaultPolicyName.ShouldBe("CustomPolicy");
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForPublicApi_AllowsAnonymousAccess()
	{
		// Act
		var options = new AuthorizationOptions
		{
			AllowAnonymousAccess = true,
			BypassAuthorizationForTypes = new[] { "PublicQuery", "HealthCheck" },
		};

		// Assert
		options.AllowAnonymousAccess.ShouldBeTrue();
		options.BypassAuthorizationForTypes.ShouldContain("PublicQuery");
	}

	[Fact]
	public void Options_ForSecureApi_RequiresAuthorization()
	{
		// Act
		var options = new AuthorizationOptions
		{
			Enabled = true,
			AllowAnonymousAccess = false,
			DefaultPolicyName = "SecureAccess",
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.AllowAnonymousAccess.ShouldBeFalse();
		options.DefaultPolicyName.ShouldBe("SecureAccess");
	}

	#endregion
}
