// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Configuration;
using Excalibur.Hosting.Configuration.Validators;

namespace Excalibur.Hosting.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="ConfigurationValidationExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class ConfigurationValidationExtensionsShould : UnitTestBase
{
	[Fact]
	public void RegisterConfigurationValidationServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddConfigurationValidation();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(ConfigurationValidationOptions));
		services.ShouldContain(sd => sd.ServiceType == typeof(IHostedService));
	}

	[Fact]
	public void RegisterConfigurationValidationWithCustomOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddConfigurationValidation(opts =>
		{
			opts.Enabled = false;
			opts.FailFast = false;
		});

		// Assert
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<ConfigurationValidationOptions>();
		options.Enabled.ShouldBeFalse();
		options.FailFast.ShouldBeFalse();
	}

	[Fact]
	public void RegisterGenericConfigurationValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddConfigurationValidator<AwsConfigurationValidator>();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IConfigurationValidator) &&
			sd.ImplementationType == typeof(AwsConfigurationValidator));
	}

	[Fact]
	public void RegisterConfigurationValidatorInstance()
	{
		// Arrange
		var services = new ServiceCollection();
		var validator = A.Fake<IConfigurationValidator>();

		// Act
		services.AddConfigurationValidator(validator);

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IConfigurationValidator));
	}

	[Fact]
	public void ThrowWhenServicesIsNullForAddConfigurationValidation()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddConfigurationValidation());
	}

	[Fact]
	public void ThrowWhenServicesIsNullForAddConfigurationValidator()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddConfigurationValidator<AwsConfigurationValidator>());
	}

	[Fact]
	public void ThrowWhenServicesIsNullForAddConfigurationValidatorInstance()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddConfigurationValidator(A.Fake<IConfigurationValidator>()));
	}

	[Fact]
	public void ThrowWhenValidatorInstanceIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddConfigurationValidator(null!));
	}

	[Fact]
	public void RegisterConnectionStringValidation()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddConnectionStringValidation("DefaultConnection", DatabaseProvider.SqlServer);

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IConfigurationValidator));
	}

	[Fact]
	public void ThrowWhenConnectionStringKeyIsNullOrWhitespace()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddConnectionStringValidation("", DatabaseProvider.SqlServer));
	}

	[Fact]
	public void RegisterAwsConfigurationValidation()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAwsConfigurationValidation();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IConfigurationValidator));
	}

	[Fact]
	public void RegisterAzureConfigurationValidation()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAzureConfigurationValidation();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IConfigurationValidator));
	}

	[Fact]
	public void RegisterGoogleCloudConfigurationValidation()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddGoogleCloudConfigurationValidation();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IConfigurationValidator));
	}

	[Fact]
	public void RegisterRabbitMqConfigurationValidation()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddRabbitMqConfigurationValidation();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IConfigurationValidator));
	}

	[Fact]
	public void RegisterKafkaConfigurationValidation()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddKafkaConfigurationValidation();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IConfigurationValidator));
	}

	[Fact]
	public void RegisterExcaliburConfigurationValidationWithDefaults()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburConfigurationValidation();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(ConfigurationValidationOptions));
	}

	[Fact]
	public void RegisterExcaliburConfigurationValidationWithAwsEnabled()
	{
		// Arrange
		var services = new ServiceCollection();
		var options = new ExcaliburValidationOptions
		{
			ValidateCloudProviders = true,
			UseAws = true,
		};

		// Act
		services.AddExcaliburConfigurationValidation(options);

		// Assert
		var validatorCount = services.Count(sd => sd.ServiceType == typeof(IConfigurationValidator));
		validatorCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public void RegisterExcaliburConfigurationValidationWithAzureEnabled()
	{
		// Arrange
		var services = new ServiceCollection();
		var options = new ExcaliburValidationOptions
		{
			ValidateCloudProviders = true,
			UseAzure = true,
		};

		// Act
		services.AddExcaliburConfigurationValidation(options);

		// Assert
		var validatorCount = services.Count(sd => sd.ServiceType == typeof(IConfigurationValidator));
		validatorCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public void RegisterExcaliburConfigurationValidationWithGoogleCloudEnabled()
	{
		// Arrange
		var services = new ServiceCollection();
		var options = new ExcaliburValidationOptions
		{
			ValidateCloudProviders = true,
			UseGoogleCloud = true,
		};

		// Act
		services.AddExcaliburConfigurationValidation(options);

		// Assert
		var validatorCount = services.Count(sd => sd.ServiceType == typeof(IConfigurationValidator));
		validatorCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public void RegisterExcaliburConfigurationValidationWithRabbitMqEnabled()
	{
		// Arrange
		var services = new ServiceCollection();
		var options = new ExcaliburValidationOptions
		{
			ValidateMessageBrokers = true,
			UseRabbitMq = true,
		};

		// Act
		services.AddExcaliburConfigurationValidation(options);

		// Assert
		var validatorCount = services.Count(sd => sd.ServiceType == typeof(IConfigurationValidator));
		validatorCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public void RegisterExcaliburConfigurationValidationWithKafkaEnabled()
	{
		// Arrange
		var services = new ServiceCollection();
		var options = new ExcaliburValidationOptions
		{
			ValidateMessageBrokers = true,
			UseKafka = true,
		};

		// Act
		services.AddExcaliburConfigurationValidation(options);

		// Assert
		var validatorCount = services.Count(sd => sd.ServiceType == typeof(IConfigurationValidator));
		validatorCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public void RegisterDatabaseValidationWhenConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		var options = new ExcaliburValidationOptions
		{
			ValidateDatabases = true,
		};
		options.DatabaseConnections.Add("Default", DatabaseProvider.SqlServer);

		// Act
		services.AddExcaliburConfigurationValidation(options);

		// Assert
		var validatorCount = services.Count(sd => sd.ServiceType == typeof(IConfigurationValidator));
		validatorCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public void UseConfigurationValidationOnHostBuilder()
	{
		// Arrange
		var hostBuilder = A.Fake<IHostBuilder>();
		var capturedAction = (Action<HostBuilderContext, IServiceCollection>?)null;
		A.CallTo(() => hostBuilder.ConfigureServices(A<Action<HostBuilderContext, IServiceCollection>>._))
			.Invokes((Action<HostBuilderContext, IServiceCollection> action) => capturedAction = action)
			.Returns(hostBuilder);

		// Act
		hostBuilder.UseConfigurationValidation();

		// Assert
		capturedAction.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenHostBuilderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IHostBuilder)null!).UseConfigurationValidation());
	}

	[Fact]
	public void UseConfigurationValidationOnHostApplicationBuilder()
	{
		// Arrange
		var builder = Host.CreateApplicationBuilder();

		// Act
		builder.UseConfigurationValidation();

		// Assert
		builder.Services.ShouldContain(sd => sd.ServiceType == typeof(ConfigurationValidationOptions));
	}

	[Fact]
	public void ThrowWhenHostApplicationBuilderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((HostApplicationBuilder)null!).UseConfigurationValidation());
	}
}
