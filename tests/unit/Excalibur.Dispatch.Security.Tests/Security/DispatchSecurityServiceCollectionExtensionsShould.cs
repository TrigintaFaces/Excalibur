// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Security.Tests.Security;

/// <summary>
/// Unit tests for <see cref="DispatchSecurityServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "DI")]
public sealed class DispatchSecurityServiceCollectionExtensionsShould
{
    [Fact]
    public void RegisterSecureCredentialManagement()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();

        // Act
        services.AddSecureCredentialManagement(config);

        // Assert
        services.ShouldContain(sd => sd.ServiceType == typeof(ICredentialStore));
        services.ShouldContain(sd => sd.ServiceType == typeof(ISecureCredentialProvider));
    }

    [Fact]
    public void RegisterInputValidation()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();

        // Act
        services.AddInputValidation(config);

        // Assert
        services.ShouldContain(sd => sd.ServiceType == typeof(IDispatchMiddleware) &&
            sd.ImplementationType == typeof(InputValidationMiddleware));
        services.ShouldContain(sd => sd.ServiceType == typeof(IInputValidator));
    }

    [Fact]
    public void RegisterAllDefaultValidators()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();

        // Act
        services.AddInputValidation(config);

        // Assert - should register all 6 default validators
        var validatorRegistrations = services.Where(sd => sd.ServiceType == typeof(IInputValidator)).ToList();
        validatorRegistrations.Count.ShouldBeGreaterThanOrEqualTo(6);
    }

    [Fact]
    public void RegisterSecurityAuditing()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();

        // Act
        services.AddSecurityAuditing(config);

        // Assert
        services.ShouldContain(sd => sd.ServiceType == typeof(ISecurityEventLogger));
        services.ShouldContain(sd => sd.ServiceType == typeof(ISecurityEventStore));
    }

    [Fact]
    public void RegisterInMemoryEventStoreByDefault()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();

        // Act
        services.AddSecurityAuditing(config);

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(ISecurityEventStore) &&
            sd.ImplementationType == typeof(InMemorySecurityEventStore));
    }

    [Fact]
    public void RegisterSqlEventStoreWhenConfigured()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:Auditing:StoreType"] = "SQL",
            })
            .Build();

        // Act
        services.AddSecurityAuditing(config);

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(ISecurityEventStore) &&
            sd.ImplementationType == typeof(SqlSecurityEventStore));
    }

    [Fact]
    public void RegisterFileEventStoreWhenConfigured()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:Auditing:StoreType"] = "FILE",
            })
            .Build();

        // Act
        services.AddSecurityAuditing(config);

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(ISecurityEventStore) &&
            sd.ImplementationType == typeof(FileSecurityEventStore));
    }

    [Fact]
    public void RegisterElasticsearchEventStoreWhenConfigured()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:Auditing:StoreType"] = "ELASTICSEARCH",
            })
            .Build();

        // Act
        services.AddSecurityAuditing(config);

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(ISecurityEventStore) &&
            sd.ImplementationType == typeof(ElasticsearchSecurityEventStore));
    }

    [Fact]
    public void RegisterCloudProviderSecurityValidators()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCloudProviderSecurityValidators();

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(Microsoft.Extensions.Options.IValidateOptions<RabbitMqOptions>));
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(Microsoft.Extensions.Options.IValidateOptions<AwsSqsOptions>));
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(Microsoft.Extensions.Options.IValidateOptions<KafkaOptions>));
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(Microsoft.Extensions.Options.IValidateOptions<GooglePubSubOptions>));
    }

    [Fact]
    public void RegisterHashiCorpVaultWhenConfigured()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Vault:Url"] = "https://vault.example.com",
            })
            .Build();

        // Act
        services.AddSecureCredentialManagement(config);

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IWritableCredentialStore) &&
            sd.ImplementationType == typeof(HashiCorpVaultCredentialStore));
    }
}
