// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox;
using Excalibur.Outbox.DynamoDb;

namespace Excalibur.Data.Tests.DynamoDb.EntryPoints;

/// <summary>
/// Unit tests for <see cref="OutboxBuilderDynamoDbExtensions.UseDynamoDb(IOutboxBuilder, Action{IDynamoDBOutboxBuilder})"/>.
/// Validates null guards, fluent chaining, configure invocation, and options registration
/// for the <c>Action&lt;IDynamoDBOutboxBuilder&gt;</c> entry point.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "DynamoDB")]
public sealed class OutboxBuilderDynamoDbExtensionsShould : UnitTestBase
{
    private const string TestServiceUrl = "http://localhost:8000";

    [Fact]
    public void UseDynamoDb_ThrowWhenBuilderIsNull()
    {
        // Arrange
        IOutboxBuilder builder = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseDynamoDb(dynamo =>
                dynamo.ServiceUrl(TestServiceUrl)));
    }

    [Fact]
    public void UseDynamoDb_ThrowWhenConfigureIsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IOutboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseDynamoDb((Action<IDynamoDBOutboxBuilder>)null!));
    }

    [Fact]
    public void UseDynamoDb_ReturnSameBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IOutboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        var result = builder.UseDynamoDb(dynamo =>
            dynamo.ServiceUrl(TestServiceUrl));

        // Assert
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void UseDynamoDb_InvokeConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IOutboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);
        var configureInvoked = false;

        // Act
        builder.UseDynamoDb(dynamo =>
        {
            dynamo.ServiceUrl(TestServiceUrl);
            configureInvoked = true;
        });

        // Assert
        configureInvoked.ShouldBeTrue();
    }

    [Fact]
    public void UseDynamoDb_RegisterOutboxOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IOutboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseDynamoDb(dynamo =>
            dynamo.ServiceUrl(TestServiceUrl));

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<DynamoDbOutboxOptions>));
    }

    [Fact]
    public void UseDynamoDb_ConfiguresTableNameViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IOutboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseDynamoDb(dynamo => dynamo
            .ServiceUrl(TestServiceUrl)
            .TableName("my_outbox"));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<DynamoDbOutboxOptions>>();
        options.Value.TableName.ShouldBe("my_outbox");
    }
}
