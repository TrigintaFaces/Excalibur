// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Postgres;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.Postgres.Builders.Saga;

/// <summary>
/// Unit tests for <see cref="PostgresSagaOptionsValidator"/>.
/// Validates the connection-aware ValidateOnStart logic.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Postgres")]
public sealed class PostgresSagaOptionsValidatorShould : UnitTestBase
{
    [Fact]
    public void Succeed_WhenConnectionStringIsSet()
    {
        var validator = new PostgresSagaOptionsValidator { HasBuilderConnection = false };
        var options = new PostgresSagaOptions { ConnectionString = "Host=localhost;" };

        var result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Succeed_WhenHasBuilderConnection()
    {
        var validator = new PostgresSagaOptionsValidator { HasBuilderConnection = true };
        var options = new PostgresSagaOptions { ConnectionString = string.Empty };

        var result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Fail_WhenNoConnectionAndNoBuilderConnection(string? connectionString)
    {
        var validator = new PostgresSagaOptionsValidator { HasBuilderConnection = false };
        var options = new PostgresSagaOptions { ConnectionString = connectionString! };

        var result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("No connection configured for Saga");
    }
}
