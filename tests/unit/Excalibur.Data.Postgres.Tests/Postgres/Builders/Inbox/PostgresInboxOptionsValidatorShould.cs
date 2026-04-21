// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.Postgres;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.Postgres.Builders.Inbox;

/// <summary>
/// Unit tests for <see cref="PostgresInboxOptionsValidator"/>.
/// Validates all validation branches including connection, schema, table, timeout, and retry.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Postgres")]
public sealed class PostgresInboxOptionsValidatorShould : UnitTestBase
{
    private static PostgresInboxOptions CreateValidOptions() => new()
    {
        ConnectionString = "Host=localhost;",
        SchemaName = "public",
        TableName = "inbox_messages",
        CommandTimeoutSeconds = 30,
        MaxRetryCount = 3
    };

    [Fact]
    public void Succeed_WhenAllOptionsAreValid()
    {
        var validator = new PostgresInboxOptionsValidator { HasBuilderConnection = false };
        var options = CreateValidOptions();

        var result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Succeed_WhenHasBuilderConnectionAndNoConnectionString()
    {
        var validator = new PostgresInboxOptionsValidator { HasBuilderConnection = true };
        var options = CreateValidOptions();
        options.ConnectionString = string.Empty;

        var result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Fail_WhenNoConnectionAndNoBuilderConnection(string connectionString)
    {
        var validator = new PostgresInboxOptionsValidator { HasBuilderConnection = false };
        var options = CreateValidOptions();
        options.ConnectionString = connectionString;

        var result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("No connection configured for Inbox");
    }

    [Fact]
    public void Fail_WhenOptionsIsNull()
    {
        var validator = new PostgresInboxOptionsValidator { HasBuilderConnection = false };

        var result = validator.Validate(null, null!);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("cannot be null");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Fail_WhenSchemaNameIsInvalid(string schema)
    {
        var validator = new PostgresInboxOptionsValidator { HasBuilderConnection = false };
        var options = CreateValidOptions();
        options.SchemaName = schema;

        var result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("SchemaName");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Fail_WhenTableNameIsInvalid(string tableName)
    {
        var validator = new PostgresInboxOptionsValidator { HasBuilderConnection = false };
        var options = CreateValidOptions();
        options.TableName = tableName;

        var result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("TableName");
    }

    [Fact]
    public void Fail_WhenCommandTimeoutIsZero()
    {
        var validator = new PostgresInboxOptionsValidator { HasBuilderConnection = false };
        var options = CreateValidOptions();
        options.CommandTimeoutSeconds = 0;

        var result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("CommandTimeoutSeconds");
    }

    [Fact]
    public void Fail_WhenMaxRetryCountIsNegative()
    {
        var validator = new PostgresInboxOptionsValidator { HasBuilderConnection = false };
        var options = CreateValidOptions();
        options.MaxRetryCount = -1;

        var result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("MaxRetryCount");
    }

    [Fact]
    public void Succeed_WhenMaxRetryCountIsZero()
    {
        var validator = new PostgresInboxOptionsValidator { HasBuilderConnection = false };
        var options = CreateValidOptions();
        options.MaxRetryCount = 0;

        var result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }
}
