// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security.Azure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Security.Tests.Azure;

/// <summary>
/// Unit tests for Azure security DI extension methods.
/// Verifies Sprint 390 implementation: DI extensions for Azure security services.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DispatchSecurityAzureServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddAzureKeyVaultCredentialStore_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;
		var configuration = CreateEmptyConfiguration();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(
			() => services.AddAzureKeyVaultCredentialStore(configuration));
		exception.ParamName.ShouldBe("services");
	}

	[Fact]
	public void AddAzureKeyVaultCredentialStore_ThrowsArgumentNullException_WhenConfigurationIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(
			() => services.AddAzureKeyVaultCredentialStore(null!));
		exception.ParamName.ShouldBe("configuration");
	}

	[Fact]
	public void AddAzureKeyVaultCredentialStore_DoesNotRegisterServices_WhenVaultUriNotConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = CreateEmptyConfiguration();

		// Act
		_ = services.AddAzureKeyVaultCredentialStore(configuration);

		// Assert - No services should be registered when VaultUri is empty
		services.ShouldNotContain(s => s.ServiceType == typeof(ICredentialStore));
		services.ShouldNotContain(s => s.ServiceType == typeof(IWritableCredentialStore));
	}

	[Fact]
	public void AddAzureKeyVaultCredentialStore_RegistersCredentialStore_WhenVaultUriConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = CreateConfigurationWithVaultUri();
		_ = services.AddSingleton(configuration);
		_ = services.AddLogging();

		// Act
		_ = services.AddAzureKeyVaultCredentialStore(configuration);

		// Assert
		services.ShouldContain(s => s.ServiceType == typeof(ICredentialStore));
	}

	[Fact]
	public void AddAzureKeyVaultCredentialStore_RegistersWritableCredentialStore_WhenVaultUriConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = CreateConfigurationWithVaultUri();
		_ = services.AddSingleton(configuration);
		_ = services.AddLogging();

		// Act
		_ = services.AddAzureKeyVaultCredentialStore(configuration);

		// Assert
		services.ShouldContain(s => s.ServiceType == typeof(IWritableCredentialStore));
	}

	[Fact]
	public void AddAzureKeyVaultCredentialStore_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = CreateEmptyConfiguration();

		// Act
		var result = services.AddAzureKeyVaultCredentialStore(configuration);

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddAzureServiceBusSecurityValidation_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(
			() => services.AddAzureServiceBusSecurityValidation());
		exception.ParamName.ShouldBe("services");
	}

	[Fact]
	public void AddAzureServiceBusSecurityValidation_RegistersOptionsValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddAzureServiceBusSecurityValidation();

		// Assert
		services.ShouldContain(s =>
			s.ServiceType.IsGenericType &&
			s.ServiceType.GetGenericTypeDefinition() == typeof(IValidateOptions<>));
	}

	[Fact]
	public void AddAzureServiceBusSecurityValidation_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddAzureServiceBusSecurityValidation();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddDispatchSecurityAzure_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;
		var configuration = CreateEmptyConfiguration();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(
			() => services.AddDispatchSecurityAzure(configuration));
		exception.ParamName.ShouldBe("services");
	}

	[Fact]
	public void AddDispatchSecurityAzure_ThrowsArgumentNullException_WhenConfigurationIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(
			() => services.AddDispatchSecurityAzure(null!));
		exception.ParamName.ShouldBe("configuration");
	}

	[Fact]
	public void AddDispatchSecurityAzure_RegistersAllServices_WhenVaultUriConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = CreateConfigurationWithVaultUri();
		_ = services.AddSingleton(configuration);
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatchSecurityAzure(configuration);

		// Assert - Should register both credential store and validator
		services.ShouldContain(s => s.ServiceType == typeof(ICredentialStore));
		services.ShouldContain(s => s.ServiceType == typeof(IWritableCredentialStore));
		services.ShouldContain(s =>
			s.ServiceType.IsGenericType &&
			s.ServiceType.GetGenericTypeDefinition() == typeof(IValidateOptions<>));
	}

	[Fact]
	public void AddDispatchSecurityAzure_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = CreateEmptyConfiguration();

		// Act
		var result = services.AddDispatchSecurityAzure(configuration);

		// Assert
		result.ShouldBeSameAs(services);
	}

	private static IConfiguration CreateEmptyConfiguration()
	{
		return new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>())
			.Build();
	}

	private static IConfiguration CreateConfigurationWithVaultUri()
	{
		return new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AzureKeyVault:VaultUri"] = "https://test-vault.vault.azure.net/"
			})
			.Build();
	}
}
