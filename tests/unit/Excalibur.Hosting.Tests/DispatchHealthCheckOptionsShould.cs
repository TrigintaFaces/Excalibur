// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Options;

namespace Excalibur.Hosting.Tests;

/// <summary>
/// Unit tests for <see cref="DispatchHealthCheckOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "HealthChecks")]
public sealed class DispatchHealthCheckOptionsShould
{
	[Fact]
	public void HaveIncludeOutboxTrueByDefault()
	{
		// Act
		var options = new DispatchHealthCheckOptions();

		// Assert
		options.IncludeOutbox.ShouldBeTrue();
	}

	[Fact]
	public void HaveIncludeInboxTrueByDefault()
	{
		// Act
		var options = new DispatchHealthCheckOptions();

		// Assert
		options.IncludeInbox.ShouldBeTrue();
	}

	[Fact]
	public void HaveIncludeSagaTrueByDefault()
	{
		// Act
		var options = new DispatchHealthCheckOptions();

		// Assert
		options.IncludeSaga.ShouldBeTrue();
	}

	[Fact]
	public void HaveIncludeLeaderElectionTrueByDefault()
	{
		// Act
		var options = new DispatchHealthCheckOptions();

		// Assert
		options.IncludeLeaderElection.ShouldBeTrue();
	}

	[Fact]
	public void AllowDisablingIndividualChecks()
	{
		// Arrange
		var options = new DispatchHealthCheckOptions();

		// Act
		options.IncludeOutbox = false;
		options.IncludeInbox = false;
		options.IncludeSaga = false;
		options.IncludeLeaderElection = false;

		// Assert
		options.IncludeOutbox.ShouldBeFalse();
		options.IncludeInbox.ShouldBeFalse();
		options.IncludeSaga.ShouldBeFalse();
		options.IncludeLeaderElection.ShouldBeFalse();
	}
}
