// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.Tests.SqlServer;

/// <summary>
/// Tests for <see cref="SqlServerHealthBasedLeaderElectionOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class SqlServerHealthBasedLeaderElectionOptionsShould
{
	[Fact]
	public void HaveDefaultSchemaName()
	{
		// Act
		var options = new SqlServerHealthBasedLeaderElectionOptions();

		// Assert
		options.SchemaName.ShouldBe("dbo");
	}

	[Fact]
	public void HaveDefaultTableName()
	{
		// Act
		var options = new SqlServerHealthBasedLeaderElectionOptions();

		// Assert
		options.TableName.ShouldBe("LeaderElectionHealth");
	}

	[Fact]
	public void HaveAutoCreateTableTrueByDefault()
	{
		// Act
		var options = new SqlServerHealthBasedLeaderElectionOptions();

		// Assert
		options.AutoCreateTable.ShouldBeTrue();
	}

	[Fact]
	public void HaveStepDownWhenUnhealthyTrueByDefault()
	{
		// Act
		var options = new SqlServerHealthBasedLeaderElectionOptions();

		// Assert
		options.StepDownWhenUnhealthy.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultHealthExpirationSeconds()
	{
		// Act
		var options = new SqlServerHealthBasedLeaderElectionOptions();

		// Assert
		options.HealthExpirationSeconds.ShouldBe(60);
	}

	[Fact]
	public void HaveDefaultCommandTimeoutSeconds()
	{
		// Act
		var options = new SqlServerHealthBasedLeaderElectionOptions();

		// Assert
		options.CommandTimeoutSeconds.ShouldBe(5);
	}

	[Fact]
	public void AllowCustomSchemaName()
	{
		// Act
		var options = new SqlServerHealthBasedLeaderElectionOptions { SchemaName = "custom" };

		// Assert
		options.SchemaName.ShouldBe("custom");
	}

	[Fact]
	public void AllowCustomTableName()
	{
		// Act
		var options = new SqlServerHealthBasedLeaderElectionOptions { TableName = "CustomHealth" };

		// Assert
		options.TableName.ShouldBe("CustomHealth");
	}

	[Fact]
	public void AllowDisablingAutoCreateTable()
	{
		// Act
		var options = new SqlServerHealthBasedLeaderElectionOptions { AutoCreateTable = false };

		// Assert
		options.AutoCreateTable.ShouldBeFalse();
	}

	[Fact]
	public void AllowDisablingStepDownWhenUnhealthy()
	{
		// Act
		var options = new SqlServerHealthBasedLeaderElectionOptions { StepDownWhenUnhealthy = false };

		// Assert
		options.StepDownWhenUnhealthy.ShouldBeFalse();
	}

	[Fact]
	public void AllowCustomHealthExpirationSeconds()
	{
		// Act
		var options = new SqlServerHealthBasedLeaderElectionOptions { HealthExpirationSeconds = 120 };

		// Assert
		options.HealthExpirationSeconds.ShouldBe(120);
	}

	[Fact]
	public void AllowCustomCommandTimeoutSeconds()
	{
		// Act
		var options = new SqlServerHealthBasedLeaderElectionOptions { CommandTimeoutSeconds = 30 };

		// Assert
		options.CommandTimeoutSeconds.ShouldBe(30);
	}
}
