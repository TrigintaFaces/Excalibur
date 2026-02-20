// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcHealthCheckOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Feature", "CDC")]
public sealed class CdcHealthCheckOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveDegradedLagThresholdOf1000_ByDefault()
	{
		// Arrange & Act
		var options = new CdcHealthCheckOptions();

		// Assert
		options.DegradedLagThreshold.ShouldBe(1000);
	}

	[Fact]
	public void HaveUnhealthyLagThresholdOf10000_ByDefault()
	{
		// Arrange & Act
		var options = new CdcHealthCheckOptions();

		// Assert
		options.UnhealthyLagThreshold.ShouldBe(10000);
	}

	[Fact]
	public void HaveUnhealthyInactivityTimeoutOfTenMinutes_ByDefault()
	{
		// Arrange & Act
		var options = new CdcHealthCheckOptions();

		// Assert
		options.UnhealthyInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void HaveDegradedInactivityTimeoutOfFiveMinutes_ByDefault()
	{
		// Arrange & Act
		var options = new CdcHealthCheckOptions();

		// Assert
		options.DegradedInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void AllowSettingDegradedLagThreshold()
	{
		// Arrange & Act
		var options = new CdcHealthCheckOptions
		{
			DegradedLagThreshold = 500
		};

		// Assert
		options.DegradedLagThreshold.ShouldBe(500);
	}

	[Fact]
	public void AllowSettingUnhealthyLagThreshold()
	{
		// Arrange & Act
		var options = new CdcHealthCheckOptions
		{
			UnhealthyLagThreshold = 50000
		};

		// Assert
		options.UnhealthyLagThreshold.ShouldBe(50000);
	}

	[Fact]
	public void AllowSettingUnhealthyInactivityTimeout()
	{
		// Arrange
		var timeout = TimeSpan.FromMinutes(30);

		// Act
		var options = new CdcHealthCheckOptions
		{
			UnhealthyInactivityTimeout = timeout
		};

		// Assert
		options.UnhealthyInactivityTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void AllowSettingDegradedInactivityTimeout()
	{
		// Arrange
		var timeout = TimeSpan.FromMinutes(2);

		// Act
		var options = new CdcHealthCheckOptions
		{
			DegradedInactivityTimeout = timeout
		};

		// Assert
		options.DegradedInactivityTimeout.ShouldBe(timeout);
	}
}
