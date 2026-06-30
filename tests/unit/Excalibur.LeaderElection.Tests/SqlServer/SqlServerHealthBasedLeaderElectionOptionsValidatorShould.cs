// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.Tests.SqlServer;

/// <summary>
/// Confirms <see cref="SqlServerHealthBasedLeaderElectionOptionsValidator"/> actually fires (rl25og): the validator
/// was wired with <c>ValidateOnStart</c> but had no implementation, so the constraints were inert. These locks are
/// non-vacuous — each bad field is rejected and a valid configuration passes.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SqlServerHealthBasedLeaderElectionOptionsValidatorShould
{
	private static readonly SqlServerHealthBasedLeaderElectionOptionsValidator Validator = new();

	private static SqlServerHealthBasedLeaderElectionOptions Valid() => new()
	{
		SchemaName = "dbo",
		TableName = "LeaderElectionHealth",
		HealthExpirationSeconds = 60,
		CommandTimeoutSeconds = 5,
	};

	[Fact]
	public void Succeed_for_a_valid_configuration()
	{
		Validator.Validate(name: null, Valid()).Succeeded.ShouldBeTrue();
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Reject_an_empty_schema_name(string schema)
	{
		var options = Valid();
		options.SchemaName = schema;
		Validator.Validate(name: null, options).Failed.ShouldBeTrue();
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Reject_an_empty_table_name(string table)
	{
		var options = Valid();
		options.TableName = table;
		Validator.Validate(name: null, options).Failed.ShouldBeTrue();
	}

	[Theory]
	[InlineData(4)]
	[InlineData(3601)]
	public void Reject_a_health_expiration_out_of_range(int seconds)
	{
		var options = Valid();
		options.HealthExpirationSeconds = seconds;
		Validator.Validate(name: null, options).Failed.ShouldBeTrue();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(301)]
	public void Reject_a_command_timeout_out_of_range(int seconds)
	{
		var options = Valid();
		options.CommandTimeoutSeconds = seconds;
		Validator.Validate(name: null, options).Failed.ShouldBeTrue();
	}
}
