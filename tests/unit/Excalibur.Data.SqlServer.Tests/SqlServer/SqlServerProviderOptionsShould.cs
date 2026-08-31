// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer;

namespace Excalibur.Data.Tests.SqlServer;

/// <summary>
/// Unit tests for SqlServerProviderOptions configuration.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "SqlServerProviderOptions")]
public sealed class SqlServerProviderOptionsShould : UnitTestBase
{
	[Fact]
	public void EnableMarsCanBeSet()
	{
		// Arrange
		var options = new SqlServerProviderOptions();

		// Act
		options.EnableMars = true;

		// Assert
		options.EnableMars.ShouldBeTrue();
	}

	[Fact]
	public void CommandTimeoutCanBeCustomized()
	{
		// Arrange
		var options = new SqlServerProviderOptions();

		// Act
		options.CommandTimeout = 60;

		// Assert
		options.CommandTimeout.ShouldBe(60);
	}

	[Fact]
	public void RetryCountCanBeCustomized()
	{
		// Arrange
		var options = new SqlServerProviderOptions();

		// Act
		options.RetryCount = 5;

		// Assert
		options.RetryCount.ShouldBe(5);
	}
}
