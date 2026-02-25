// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization.Grants;

namespace Excalibur.Tests.A3.Grants;

/// <summary>
/// Unit tests for <see cref="GrantType"/> static class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class GrantTypeShould : UnitTestBase
{
	[Fact]
	public void Activity_HasExpectedValue()
	{
		// Assert
		GrantType.Activity.ShouldBe("Activity");
	}

	[Fact]
	public void ActivityGroup_HasExpectedValue()
	{
		// Assert
		GrantType.ActivityGroup.ShouldBe("ActivityGroup");
	}

	[Fact]
	public void Activity_IsNotNullOrEmpty()
	{
		// Assert
		GrantType.Activity.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void ActivityGroup_IsNotNullOrEmpty()
	{
		// Assert
		GrantType.ActivityGroup.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void Activity_AndActivityGroup_AreDifferent()
	{
		// Assert
		GrantType.Activity.ShouldNotBe(GrantType.ActivityGroup);
	}
}
