// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Suppress for test - RequiresUnreferencedCode/RequiresDynamicCode

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Dispatch.Security.Tests.Security;

/// <summary>
/// Depth tests for <see cref="DispatchSecurityServiceCollectionExtensions"/>.
/// Covers the composite AddDispatchSecurity overload, null argument guards,
/// HashiCorp Vault conditional registration, auditing store type selection,
/// and UseSecurityMiddleware builder extension.
/// </summary>
/// <remarks>
/// Both <see cref="DispatchSecurityServiceCollectionExtensions"/> and
/// <see cref="SecurityMiddlewareExtensions"/> define an AddDispatchSecurity
/// overload taking IConfiguration. Because the test namespace imports
/// <c>Excalibur.Dispatch.Security</c>, the extension method from
/// <see cref="SecurityMiddlewareExtensions"/> would win via normal extension
/// resolution. Tests that target the <c>DispatchSecurityServiceCollectionExtensions</c>
/// overload therefore use a fully-qualified static call.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "DI")]
public sealed class DispatchSecurityServiceCollectionExtensionsDepthShould
{
	[Fact]
	public void AddDispatchSecurity_ThrowsWhenServicesIsNull()
	{
		var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();

		Should.Throw<ArgumentNullException>(() =>
			DispatchSecurityServiceCollectionExtensions.AddDispatchSecurity(null!, config));
	}

	[Fact]
	public void AddDispatchSecurity_ThrowsWhenConfigurationIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			DispatchSecurityServiceCollectionExtensions.AddDispatchSecurity(services, (IConfiguration)null!));
	}

	[Fact]
	public void AddDispatchSecurity_RegistersAllComponents()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection([])
			.Build();

		// Act — use fully-qualified call to target DispatchSecurityServiceCollectionExtensions
		DispatchSecurityServiceCollectionExtensions.AddDispatchSecurity(services, config);

		// Assert — credential management, input validation, auditing, cloud validators
		services.ShouldContain(sd => sd.ServiceType == typeof(ISecureCredentialProvider));
		services.ShouldContain(sd => sd.ServiceType == typeof(ISecurityEventLogger));
		services.ShouldContain(sd => sd.ServiceType == typeof(ISecurityEventStore));
		services.ShouldContain(sd => sd.ServiceType == typeof(IInputValidator));
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IValidateOptions<RabbitMqOptions>));
	}

	[Fact]
	public void AddSecureCredentialManagement_RegistersHashiCorpVaultWhenConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Vault:Url"] = "https://vault.example.com:8200",
			})
			.Build();

		// Act
		services.AddSecureCredentialManagement(config);

		// Assert — should register both ICredentialStore and IWritableCredentialStore for HashiCorp
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICredentialStore) &&
			sd.ImplementationType == typeof(HashiCorpVaultCredentialStore));
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IWritableCredentialStore) &&
			sd.ImplementationType == typeof(HashiCorpVaultCredentialStore));
	}

	[Fact]
	public void AddSecureCredentialManagement_SkipsHashiCorpVaultWhenNotConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection([])
			.Build();

		// Act
		services.AddSecureCredentialManagement(config);

		// Assert — no HashiCorp Vault stores registered
		services.ShouldNotContain(sd =>
			sd.ImplementationType == typeof(HashiCorpVaultCredentialStore));
	}

	[Fact]
	public void AddSecureCredentialManagement_AlwaysRegistersEnvironmentVariableStore()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();

		// Act
		services.AddSecureCredentialManagement(config);

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICredentialStore) &&
			sd.ImplementationType == typeof(EnvironmentVariableCredentialStore));
	}

	[Fact]
	public void AddInputValidation_RegistersAllSixDefaultValidators()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();

		// Act
		services.AddInputValidation(config);

		// Assert — should register SqlInjection, Xss, PathTraversal, CommandInjection, DataSize, MessageAge
		var validatorTypes = services
			.Where(sd => sd.ServiceType == typeof(IInputValidator))
			.Select(sd => sd.ImplementationType)
			.ToList();

		validatorTypes.ShouldContain(typeof(SqlInjectionValidator));
		validatorTypes.ShouldContain(typeof(XssValidator));
		validatorTypes.ShouldContain(typeof(PathTraversalValidator));
		validatorTypes.ShouldContain(typeof(CommandInjectionValidator));
		validatorTypes.ShouldContain(typeof(DataSizeValidator));
		validatorTypes.ShouldContain(typeof(MessageAgeValidator));
	}

	[Fact]
	public void AddInputValidation_RegistersInputValidationMiddleware()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();

		// Act
		services.AddInputValidation(config);

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDispatchMiddleware) &&
			sd.ImplementationType == typeof(InputValidationMiddleware));
	}

	[Fact]
	public void AddSecurityAuditing_RegistersSecurityEventLoggerAsHostedService()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();

		// Act
		services.AddSecurityAuditing(config);

		// Assert — SecurityEventLogger registered as both ISecurityEventLogger and IHostedService
		services.ShouldContain(sd => sd.ServiceType == typeof(ISecurityEventLogger));
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService));
	}

	[Fact]
	public void AddSecurityAuditing_RegistersSqlStoreWhenConfiguredCaseInsensitive()
	{
		// Arrange — test case-insensitivity
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Security:Auditing:StoreType"] = "sql",
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
	public void AddSecurityAuditing_RegistersElasticsearchStoreWhenConfiguredCaseInsensitive()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Security:Auditing:StoreType"] = "elasticsearch",
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
	public void AddSecurityAuditing_RegistersFileStoreWhenConfiguredCaseInsensitive()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Security:Auditing:StoreType"] = "file",
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
	public void AddSecurityAuditing_DefaultsToInMemoryForUnknownStoreType()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Security:Auditing:StoreType"] = "UNKNOWN",
			})
			.Build();

		// Act
		services.AddSecurityAuditing(config);

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ISecurityEventStore) &&
			sd.ImplementationType == typeof(InMemorySecurityEventStore));
	}

	[Fact]
	public void AddCloudProviderSecurityValidators_RegistersAllFourValidators()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCloudProviderSecurityValidators();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IValidateOptions<RabbitMqOptions>));
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IValidateOptions<AwsSqsOptions>));
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IValidateOptions<KafkaOptions>));
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IValidateOptions<GooglePubSubOptions>));
	}

	[Fact]
	public void AddCloudProviderSecurityValidators_DoesNotDuplicateOnMultipleCalls()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act — call twice
		services.AddCloudProviderSecurityValidators();
		services.AddCloudProviderSecurityValidators();

		// Assert — TryAddEnumerable prevents duplicates
		var rabbitMqValidators = services
			.Where(sd => sd.ServiceType == typeof(IValidateOptions<RabbitMqOptions>))
			.ToList();
		rabbitMqValidators.Count.ShouldBe(1);
	}

	[Fact]
	public void UseSecurityMiddleware_ThrowsWhenBuilderIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			DispatchSecurityServiceCollectionExtensions.UseSecurityMiddleware(null!));
	}

	[Fact]
	public void UseSecurityMiddleware_ReturnsBuilderForChaining()
	{
		// Arrange
		var builder = A.Fake<IDispatchBuilder>();

		// Act
		var result = builder.UseSecurityMiddleware();

		// Assert
		result.ShouldBe(builder);
	}
}

#pragma warning restore IL2026, IL3050
