// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.Postgres;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.Postgres.Builders.LeaderElection;

/// <summary>
/// Unit tests for <see cref="PostgresLeaderElectionOptionsValidator"/>.
/// Validates the connection-aware ValidateOnStart logic.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Postgres")]
public sealed class PostgresLeaderElectionOptionsValidatorShould : UnitTestBase
{
    [Fact]
    public void Succeed_WhenConnectionStringIsSet()
    {
        var validator = new PostgresLeaderElectionOptionsValidator { HasBuilderConnection = false };
        var options = new PostgresLeaderElectionOptions { ConnectionString = "Host=localhost;" };

        var result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Succeed_WhenHasBuilderConnection()
    {
        var validator = new PostgresLeaderElectionOptionsValidator { HasBuilderConnection = true };
        var options = new PostgresLeaderElectionOptions { ConnectionString = string.Empty };

        var result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Fail_WhenNoConnectionAndNoBuilderConnection(string connectionString)
    {
        var validator = new PostgresLeaderElectionOptionsValidator { HasBuilderConnection = false };
        var options = new PostgresLeaderElectionOptions { ConnectionString = connectionString };

        var result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("No connection configured for LeaderElection");
    }
}
