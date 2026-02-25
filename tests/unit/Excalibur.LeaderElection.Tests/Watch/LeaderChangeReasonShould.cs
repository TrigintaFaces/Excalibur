// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.Watch;

namespace Excalibur.LeaderElection.Tests.Watch;

/// <summary>
/// Unit tests for <see cref="LeaderChangeReason"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class LeaderChangeReasonShould
{
	[Fact]
	public void HaveElectedAsZero()
	{
		// Assert
		((int)LeaderChangeReason.Elected).ShouldBe(0);
	}

	[Fact]
	public void HaveExpiredAsOne()
	{
		// Assert
		((int)LeaderChangeReason.Expired).ShouldBe(1);
	}

	[Fact]
	public void HaveResignedAsTwo()
	{
		// Assert
		((int)LeaderChangeReason.Resigned).ShouldBe(2);
	}

	[Fact]
	public void HaveHealthCheckFailedAsThree()
	{
		// Assert
		((int)LeaderChangeReason.HealthCheckFailed).ShouldBe(3);
	}

	[Fact]
	public void HaveFourDefinedValues()
	{
		// Assert
		Enum.GetValues<LeaderChangeReason>().Length.ShouldBe(4);
	}
}
