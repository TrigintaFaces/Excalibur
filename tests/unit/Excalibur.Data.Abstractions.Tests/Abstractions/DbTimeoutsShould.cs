// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="DbTimeouts"/> constants.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Feature", "Abstractions")]
public sealed class DbTimeoutsShould : UnitTestBase
{
	[Fact]
	public void HaveRegularTimeoutOfSixtySeconds()
	{
		// Assert
		DbTimeouts.RegularTimeoutSeconds.ShouldBe(60);
	}

	[Fact]
	public void HaveLongRunningTimeoutOfTenMinutes()
	{
		// Assert
		DbTimeouts.LongRunningTimeoutSeconds.ShouldBe(600);
	}

	[Fact]
	public void HaveExtraLongRunningTimeoutOfTwentyMinutes()
	{
		// Assert
		DbTimeouts.ExtraLongRunningTimeoutSeconds.ShouldBe(1200);
	}

	[Fact]
	public void HaveTimeoutsInIncreasingOrder()
	{
		// Assert
		DbTimeouts.RegularTimeoutSeconds.ShouldBeLessThan(DbTimeouts.LongRunningTimeoutSeconds);
		DbTimeouts.LongRunningTimeoutSeconds.ShouldBeLessThan(DbTimeouts.ExtraLongRunningTimeoutSeconds);
	}

	[Fact]
	public void HavePositiveTimeoutValues()
	{
		// Assert
		DbTimeouts.RegularTimeoutSeconds.ShouldBeGreaterThan(0);
		DbTimeouts.LongRunningTimeoutSeconds.ShouldBeGreaterThan(0);
		DbTimeouts.ExtraLongRunningTimeoutSeconds.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void BeUsableForTimeSpanCreation()
	{
		// Act
		var regular = TimeSpan.FromSeconds(DbTimeouts.RegularTimeoutSeconds);
		var longRunning = TimeSpan.FromSeconds(DbTimeouts.LongRunningTimeoutSeconds);
		var extraLong = TimeSpan.FromSeconds(DbTimeouts.ExtraLongRunningTimeoutSeconds);

		// Assert
		regular.ShouldBe(TimeSpan.FromMinutes(1));
		longRunning.ShouldBe(TimeSpan.FromMinutes(10));
		extraLong.ShouldBe(TimeSpan.FromMinutes(20));
	}
}
