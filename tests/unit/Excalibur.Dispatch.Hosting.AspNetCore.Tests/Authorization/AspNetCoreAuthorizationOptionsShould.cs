// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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

	/// <summary>
	/// SAFETY, and it is structural rather than behavioural on purpose. This type must NOT grow a policy
	/// setting of its own. Policies are the host's: they are configured once through <c>AddAuthorization</c>
	/// and composed by the host's <c>IAuthorizationPolicyProvider</c>. A parallel knob here is a second place
	/// to configure one concern, and the two can then disagree -- which is exactly what shipped: a
	/// default-policy name on this type silently stood in for the host's default policy, so a consumer who
	/// HARDENED theirs had the hardening ignored and a bare [Authorize] collapsed to "any authenticated user".
	/// Re-adding any such member turns this arm RED.
	/// </summary>
	[Fact]
	public void CarryNoPolicyConfigurationOfItsOwn()
	{
		var settable = typeof(AspNetCoreAuthorizationOptions)
			.GetProperties()
			.Select(static p => p.Name)
			.OrderBy(static name => name, StringComparer.Ordinal)
			.ToArray();

		settable.ShouldBe(["Enabled", "RequireAuthenticatedUser"]);
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

	/// <summary>
	/// LIVENESS for the arm above. Asserting an EXACT property set would also pass if the type were emptied
	/// entirely, so the two knobs it is supposed to have must still be settable and must still hold.
	/// </summary>
	[Fact]
	public void StillCarryItsOwnTwoSwitches()
	{
		var options = new AspNetCoreAuthorizationOptions { Enabled = false, RequireAuthenticatedUser = false };

		options.Enabled.ShouldBeFalse();
		options.RequireAuthenticatedUser.ShouldBeFalse();
	}
}
