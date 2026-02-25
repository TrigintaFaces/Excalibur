// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Messaging;

/// <summary>
/// Unit tests for <see cref="AccessRule"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class AccessRuleShould
{
	[Fact]
	public void HaveEmptyPrincipal_ByDefault()
	{
		// Arrange & Act
		var rule = new AccessRule();

		// Assert
		rule.Principal.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultPermissions_ByDefault()
	{
		// Arrange & Act
		var rule = new AccessRule();

		// Assert
		rule.Permissions.ShouldBe(default(AccessPermissions));
	}

	[Fact]
	public void AllowSettingPrincipal()
	{
		// Arrange
		var rule = new AccessRule();

		// Act
		rule.Principal = "user@example.com";

		// Assert
		rule.Principal.ShouldBe("user@example.com");
	}

	[Fact]
	public void AllowSettingPermissions()
	{
		// Arrange
		var rule = new AccessRule();

		// Act
		rule.Permissions = AccessPermissions.Receive;

		// Assert
		rule.Permissions.ShouldBe(AccessPermissions.Receive);
	}

	[Fact]
	public void AllowSettingMultiplePermissions()
	{
		// Arrange
		var rule = new AccessRule();

		// Act
		rule.Permissions = AccessPermissions.Receive | AccessPermissions.Send;

		// Assert
		rule.Permissions.HasFlag(AccessPermissions.Receive).ShouldBeTrue();
		rule.Permissions.HasFlag(AccessPermissions.Send).ShouldBeTrue();
	}

	[Fact]
	public void AllowCreatingFullyPopulatedRule()
	{
		// Arrange & Act
		var rule = new AccessRule
		{
			Principal = "service-account@project.iam.gserviceaccount.com",
			Permissions = AccessPermissions.Receive | AccessPermissions.Send | AccessPermissions.Manage,
		};

		// Assert
		rule.Principal.ShouldBe("service-account@project.iam.gserviceaccount.com");
		rule.Permissions.HasFlag(AccessPermissions.Receive).ShouldBeTrue();
		rule.Permissions.HasFlag(AccessPermissions.Send).ShouldBeTrue();
		rule.Permissions.HasFlag(AccessPermissions.Manage).ShouldBeTrue();
	}

	[Fact]
	public void AllowCreatingReceiveOnlyRule()
	{
		// Arrange & Act
		var rule = new AccessRule
		{
			Principal = "receive-only-user",
			Permissions = AccessPermissions.Receive,
		};

		// Assert
		rule.Principal.ShouldBe("receive-only-user");
		rule.Permissions.ShouldBe(AccessPermissions.Receive);
		rule.Permissions.HasFlag(AccessPermissions.Send).ShouldBeFalse();
	}
}
