// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing.Diagnostics;

namespace Excalibur.Data.Tests.DataProcessing.Diagnostics;

/// <summary>
/// Unit tests for <see cref="DataProcessingHealthCheckOptions"/>.
/// </summary>
[UnitTest]
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DataProcessingHealthCheckOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new DataProcessingHealthCheckOptions();

		// Assert
		options.DegradedInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		options.UnhealthyInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void AllowCustomDegradedTimeout()
	{
		// Arrange & Act
		var options = new DataProcessingHealthCheckOptions
		{
			DegradedInactivityTimeout = TimeSpan.FromMinutes(2),
		};

		// Assert
		options.DegradedInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void AllowCustomUnhealthyTimeout()
	{
		// Arrange & Act
		var options = new DataProcessingHealthCheckOptions
		{
			UnhealthyInactivityTimeout = TimeSpan.FromMinutes(30),
		};

		// Assert
		options.UnhealthyInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(30));
	}
}
