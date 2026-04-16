// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Tests.DynamoDb.Builders;

/// <summary>
/// Unit tests for <see cref="DynamoDbCdcStateStoreBuilder"/> — state store configuration,
/// no-op schema handling, argument guards, and fluent chaining.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.CDC)]
[Trait("Database", "DynamoDB")]
public sealed class DynamoDbCdcStateStoreBuilderShould : UnitTestBase
{
    private static (DynamoDbCdcStateStoreBuilder Builder, DynamoDbCdcStateStoreOptions Options) CreateBuilder()
    {
        var options = new DynamoDbCdcStateStoreOptions();
        var builder = new DynamoDbCdcStateStoreBuilder(options);
        return (builder, options);
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() => new DynamoDbCdcStateStoreBuilder(null!));
    }

    // --- Feature methods (happy path) ---

    [Fact]
    public void TableName_SetOnOptions()
    {
        var (builder, options) = CreateBuilder();

        builder.TableName("cdc_positions");

        options.TableName.ShouldBe("cdc_positions");
    }

    [Fact]
    public void BindConfiguration_StorePath()
    {
        var (builder, _) = CreateBuilder();

        builder.BindConfiguration("Cdc:StateStore");

        builder.BindConfigurationPath.ShouldBe("Cdc:StateStore");
    }

    // --- Null/invalid argument guards ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TableName_ThrowOnNullOrWhitespace(string? value)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.TableName(value!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BindConfiguration_ThrowOnNullOrWhitespace(string? value)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.BindConfiguration(value!));
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();

        var result = builder
            .TableName("cdc_positions")
            .BindConfiguration("Cdc:StateStore");

        result.ShouldBeSameAs(builder);
    }
}
