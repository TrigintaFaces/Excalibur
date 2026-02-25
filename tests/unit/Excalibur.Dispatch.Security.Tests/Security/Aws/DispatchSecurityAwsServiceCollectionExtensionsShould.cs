// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security.Aws;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Security.Tests.Aws;

/// <summary>
/// Unit tests for AWS security DI extension methods.
/// Verifies Sprint 390 implementation: DI extensions for AWS security services.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DispatchSecurityAwsServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddAwsSecretsManagerCredentialStore_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;
		var configuration = CreateEmptyConfiguration();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(
			() => services.AddAwsSecretsManagerCredentialStore(configuration));
		exception.ParamName.ShouldBe("services");
	}

	[Fact]
	public void AddAwsSecretsManagerCredentialStore_ThrowsArgumentNullException_WhenConfigurationIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(
			() => services.AddAwsSecretsManagerCredentialStore(null!));
		exception.ParamName.ShouldBe("configuration");
	}

	[Fact]
	public void AddAwsSecretsManagerCredentialStore_DoesNotRegisterServices_WhenRegionNotConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = CreateEmptyConfiguration();

		// Act
		_ = services.AddAwsSecretsManagerCredentialStore(configuration);

		// Assert - No services should be registered when Region is empty
		services.ShouldNotContain(s => s.ServiceType == typeof(ICredentialStore));
		services.ShouldNotContain(s => s.ServiceType == typeof(IWritableCredentialStore));
	}

	[Fact]
	public void AddAwsSecretsManagerCredentialStore_RegistersCredentialStore_WhenRegionConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = CreateConfigurationWithRegion();
		_ = services.AddSingleton(configuration);
		_ = services.AddLogging();

		// Act
		_ = services.AddAwsSecretsManagerCredentialStore(configuration);

		// Assert
		services.ShouldContain(s => s.ServiceType == typeof(ICredentialStore));
	}

	[Fact]
	public void AddAwsSecretsManagerCredentialStore_RegistersWritableCredentialStore_WhenRegionConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = CreateConfigurationWithRegion();
		_ = services.AddSingleton(configuration);
		_ = services.AddLogging();

		// Act
		_ = services.AddAwsSecretsManagerCredentialStore(configuration);

		// Assert
		services.ShouldContain(s => s.ServiceType == typeof(IWritableCredentialStore));
	}

	[Fact]
	public void AddAwsSecretsManagerCredentialStore_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = CreateEmptyConfiguration();

		// Act
		var result = services.AddAwsSecretsManagerCredentialStore(configuration);

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddDispatchSecurityAws_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;
		var configuration = CreateEmptyConfiguration();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(
			() => services.AddDispatchSecurityAws(configuration));
		exception.ParamName.ShouldBe("services");
	}

	[Fact]
	public void AddDispatchSecurityAws_ThrowsArgumentNullException_WhenConfigurationIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(
			() => services.AddDispatchSecurityAws(null!));
		exception.ParamName.ShouldBe("configuration");
	}

	[Fact]
	public void AddDispatchSecurityAws_RegistersAllServices_WhenRegionConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = CreateConfigurationWithRegion();
		_ = services.AddSingleton(configuration);
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatchSecurityAws(configuration);

		// Assert
		services.ShouldContain(s => s.ServiceType == typeof(ICredentialStore));
		services.ShouldContain(s => s.ServiceType == typeof(IWritableCredentialStore));
	}

	[Fact]
	public void AddDispatchSecurityAws_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = CreateEmptyConfiguration();

		// Act
		var result = services.AddDispatchSecurityAws(configuration);

		// Assert
		result.ShouldBeSameAs(services);
	}

	private static IConfiguration CreateEmptyConfiguration()
	{
		return new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>())
			.Build();
	}

	private static IConfiguration CreateConfigurationWithRegion()
	{
		return new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AWS:Region"] = "us-east-1"
			})
			.Build();
	}
}
