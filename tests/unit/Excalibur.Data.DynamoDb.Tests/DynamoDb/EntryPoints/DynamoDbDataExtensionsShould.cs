// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb;

namespace Excalibur.Data.Tests.DynamoDb.EntryPoints;

/// <summary>
/// Unit tests for <see cref="DynamoDbServiceCollectionExtensions.AddExcaliburDynamoDb(IServiceCollection, Action{IDynamoDBDataBuilder})"/>.
/// Validates null guards, fluent chaining, configure invocation, and options registration
/// for the <c>Action&lt;IDynamoDBDataBuilder&gt;</c> entry point.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "DynamoDB")]
public sealed class DynamoDbDataExtensionsShould : UnitTestBase
{
    private const string TestServiceUrl = "http://localhost:8000";

    [Fact]
    public void AddExcaliburDynamoDb_ThrowWhenServicesIsNull()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            services.AddExcaliburDynamoDb(dynamo =>
                dynamo.ServiceUrl(TestServiceUrl)));
    }

    [Fact]
    public void AddExcaliburDynamoDb_ThrowWhenConfigureIsNull()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            services.AddExcaliburDynamoDb((Action<IDynamoDBDataBuilder>)null!));
    }

    [Fact]
    public void AddExcaliburDynamoDb_ReturnSameServicesForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddExcaliburDynamoDb(dynamo =>
            dynamo.ServiceUrl(TestServiceUrl));

        // Assert
        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddExcaliburDynamoDb_InvokeConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        var configureInvoked = false;

        // Act
        services.AddExcaliburDynamoDb(dynamo =>
        {
            dynamo.ServiceUrl(TestServiceUrl);
            configureInvoked = true;
        });

        // Assert
        configureInvoked.ShouldBeTrue();
    }

    [Fact]
    public void AddExcaliburDynamoDb_RegisterDynamoDbOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddExcaliburDynamoDb(dynamo =>
            dynamo.ServiceUrl(TestServiceUrl));

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<DynamoDbOptions>));
    }

    [Fact]
    public void AddExcaliburDynamoDb_ConfiguresTableNameViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddExcaliburDynamoDb(dynamo => dynamo
            .ServiceUrl(TestServiceUrl)
            .TableName("my_data"));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<DynamoDbOptions>>();
        options.Value.DefaultTableName.ShouldBe("my_data");
    }
}
