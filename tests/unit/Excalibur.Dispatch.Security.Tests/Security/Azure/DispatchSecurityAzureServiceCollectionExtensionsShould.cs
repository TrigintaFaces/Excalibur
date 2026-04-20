// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Security.Azure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Azure;

/// <summary>
/// Unit tests for Azure security DI extension methods.
/// Verifies builder-based entry point: AddDispatchSecurityAzure(Action&lt;ISecurityAzureBuilder&gt;).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
public sealed class DispatchSecurityAzureServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddDispatchSecurityAzure_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(
			() => services.AddDispatchSecurityAzure(_ => { }));
		exception.ParamName.ShouldBe("services");
	}

	[Fact]
	public void AddDispatchSecurityAzure_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(
			() => services.AddDispatchSecurityAzure((Action<ISecurityAzureBuilder>)null!));
		exception.ParamName.ShouldBe("configure");
	}

	[Fact]
	public void AddDispatchSecurityAzure_DoesNotRegisterCredentialStore_WhenVaultUriNotConfigured()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- builder with no VaultUri call
		_ = services.AddDispatchSecurityAzure(_ => { });

		// Assert - No credential store services should be registered when VaultUri is not set
		services.ShouldNotContain(s => s.ServiceType == typeof(ICredentialStore));
		services.ShouldNotContain(s => s.ServiceType == typeof(IWritableCredentialStore));
	}

	[Fact]
	public void AddDispatchSecurityAzure_RegistersCredentialStore_WhenVaultUriConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatchSecurityAzure(azure =>
		{
			azure.VaultUri("https://test-vault.vault.azure.net/");
		});

		// Assert
		services.ShouldContain(s => s.ServiceType == typeof(ICredentialStore));
	}

	[Fact]
	public void AddDispatchSecurityAzure_RegistersWritableCredentialStore_WhenVaultUriConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatchSecurityAzure(azure =>
		{
			azure.VaultUri("https://test-vault.vault.azure.net/");
		});

		// Assert
		services.ShouldContain(s => s.ServiceType == typeof(IWritableCredentialStore));
	}

	[Fact]
	public void AddDispatchSecurityAzure_RegistersServiceBusValidator_ByDefault()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- ServiceBusValidation is enabled by default in SecurityAzureBuilder
		_ = services.AddDispatchSecurityAzure(azure =>
		{
			azure.VaultUri("https://test-vault.vault.azure.net/");
		});

		// Assert
		services.ShouldContain(s =>
			s.ServiceType.IsGenericType &&
			s.ServiceType.GetGenericTypeDefinition() == typeof(IValidateOptions<>));
	}

	[Fact]
	public void AddDispatchSecurityAzure_RegistersAllServices_WhenVaultUriConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatchSecurityAzure(azure =>
		{
			azure.VaultUri("https://test-vault.vault.azure.net/")
				.EnableServiceBusValidation();
		});

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

		// Act
		var result = services.AddDispatchSecurityAzure(_ => { });

		// Assert
		result.ShouldBeSameAs(services);
	}
}
