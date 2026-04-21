// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.Postgres;

namespace Excalibur.Data.Tests.Postgres;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class PostgresHealthBasedLeaderElectionOptionsShould
{
	[Fact]
	public void HaveDefaultSchemaName()
	{
		var options = new PostgresHealthBasedLeaderElectionOptions();
		options.SchemaName.ShouldBe("public");
	}

	[Fact]
	public void HaveDefaultTableName()
	{
		var options = new PostgresHealthBasedLeaderElectionOptions();
		options.TableName.ShouldBe("leader_election_health");
	}

	[Fact]
	public void HaveDefaultAutoCreateTable()
	{
		var options = new PostgresHealthBasedLeaderElectionOptions();
		options.AutoCreateTable.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultStepDownWhenUnhealthy()
	{
		var options = new PostgresHealthBasedLeaderElectionOptions();
		options.StepDownWhenUnhealthy.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultHealthExpirationSeconds()
	{
		var options = new PostgresHealthBasedLeaderElectionOptions();
		options.HealthExpirationSeconds.ShouldBe(60);
	}

	[Fact]
	public void HaveDefaultCommandTimeoutSeconds()
	{
		var options = new PostgresHealthBasedLeaderElectionOptions();
		options.CommandTimeoutSeconds.ShouldBe(5);
	}

	[Fact]
	public void AllowCustomSchemaName()
	{
		var options = new PostgresHealthBasedLeaderElectionOptions { SchemaName = "custom_schema" };
		options.SchemaName.ShouldBe("custom_schema");
	}

	[Fact]
	public void AllowCustomTableName()
	{
		var options = new PostgresHealthBasedLeaderElectionOptions { TableName = "custom_health" };
		options.TableName.ShouldBe("custom_health");
	}

	[Fact]
	public void AllowDisablingAutoCreateTable()
	{
		var options = new PostgresHealthBasedLeaderElectionOptions { AutoCreateTable = false };
		options.AutoCreateTable.ShouldBeFalse();
	}

	[Fact]
	public void AllowDisablingStepDownWhenUnhealthy()
	{
		var options = new PostgresHealthBasedLeaderElectionOptions { StepDownWhenUnhealthy = false };
		options.StepDownWhenUnhealthy.ShouldBeFalse();
	}

	[Fact]
	public void AllowCustomHealthExpirationSeconds()
	{
		var options = new PostgresHealthBasedLeaderElectionOptions { HealthExpirationSeconds = 120 };
		options.HealthExpirationSeconds.ShouldBe(120);
	}

	[Fact]
	public void AllowCustomCommandTimeoutSeconds()
	{
		var options = new PostgresHealthBasedLeaderElectionOptions { CommandTimeoutSeconds = 30 };
		options.CommandTimeoutSeconds.ShouldBe(30);
	}
}
