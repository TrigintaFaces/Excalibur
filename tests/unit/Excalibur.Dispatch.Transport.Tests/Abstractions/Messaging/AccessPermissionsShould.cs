// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Messaging;

/// <summary>
/// Unit tests for <see cref="AccessPermissions"/> flags enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class AccessPermissionsShould
{
	[Fact]
	public void HaveFiveDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<AccessPermissions>();

		// Assert
		values.Length.ShouldBe(5);
		values.ShouldContain(AccessPermissions.None);
		values.ShouldContain(AccessPermissions.Send);
		values.ShouldContain(AccessPermissions.Receive);
		values.ShouldContain(AccessPermissions.Manage);
		values.ShouldContain(AccessPermissions.All);
	}

	[Fact]
	public void None_HasExpectedValue()
	{
		// Assert
		((int)AccessPermissions.None).ShouldBe(0);
	}

	[Fact]
	public void Send_HasExpectedValue()
	{
		// Assert
		((int)AccessPermissions.Send).ShouldBe(1);
	}

	[Fact]
	public void Receive_HasExpectedValue()
	{
		// Assert
		((int)AccessPermissions.Receive).ShouldBe(2);
	}

	[Fact]
	public void Manage_HasExpectedValue()
	{
		// Assert
		((int)AccessPermissions.Manage).ShouldBe(4);
	}

	[Fact]
	public void All_HasExpectedValue()
	{
		// Assert - All should be Send | Receive | Manage = 1 | 2 | 4 = 7
		((int)AccessPermissions.All).ShouldBe(7);
	}

	[Fact]
	public void None_IsDefaultValue()
	{
		// Arrange
		AccessPermissions defaultPermissions = default;

		// Assert
		defaultPermissions.ShouldBe(AccessPermissions.None);
	}

	[Fact]
	public void All_CombinesAllPermissions()
	{
		// Assert - All should be equivalent to combining Send, Receive, and Manage
		var combined = AccessPermissions.Send | AccessPermissions.Receive | AccessPermissions.Manage;
		combined.ShouldBe(AccessPermissions.All);
	}

	[Theory]
	[InlineData(AccessPermissions.Send)]
	[InlineData(AccessPermissions.Receive)]
	[InlineData(AccessPermissions.Manage)]
	public void All_ContainsIndividualPermission(AccessPermissions permission)
	{
		// Assert
		AccessPermissions.All.HasFlag(permission).ShouldBeTrue();
	}

	[Fact]
	public void None_DoesNotContainAnyPermissions()
	{
		// Assert
		AccessPermissions.None.HasFlag(AccessPermissions.Send).ShouldBeFalse();
		AccessPermissions.None.HasFlag(AccessPermissions.Receive).ShouldBeFalse();
		AccessPermissions.None.HasFlag(AccessPermissions.Manage).ShouldBeFalse();
	}

	[Fact]
	public void HaveFlagsAttribute()
	{
		// Assert - Should be decorated with [Flags] attribute
		typeof(AccessPermissions).IsDefined(typeof(FlagsAttribute), false).ShouldBeTrue();
	}

	[Theory]
	[InlineData(AccessPermissions.Send | AccessPermissions.Receive)]
	[InlineData(AccessPermissions.Send | AccessPermissions.Manage)]
	[InlineData(AccessPermissions.Receive | AccessPermissions.Manage)]
	public void SupportCombinations(AccessPermissions combination)
	{
		// Assert - Combined flags should be defined
		combination.ShouldNotBe(AccessPermissions.None);
	}

	[Fact]
	public void SendAndReceive_IsValidCombination()
	{
		// Arrange
		var permissions = AccessPermissions.Send | AccessPermissions.Receive;

		// Assert
		permissions.HasFlag(AccessPermissions.Send).ShouldBeTrue();
		permissions.HasFlag(AccessPermissions.Receive).ShouldBeTrue();
		permissions.HasFlag(AccessPermissions.Manage).ShouldBeFalse();
	}
}
