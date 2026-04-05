// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing.Processing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Unit tests for <c>EnableDataProcessingBackgroundService</c> DI extension method.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class EnableDataProcessingBackgroundServiceShould : UnitTestBase
{
	[Fact]
	public void RegisterHostedService()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.EnableDataProcessingBackgroundService();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IHostedService) &&
			sd.ImplementationType == typeof(DataProcessingHostedService));
	}

	[Fact]
	public void RegisterOptionsValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.EnableDataProcessingBackgroundService();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IValidateOptions<DataProcessingHostedServiceOptions>) &&
			sd.ImplementationType == typeof(DataProcessingHostedServiceOptionsValidator));
	}

	[Fact]
	public void ApplyCustomConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.EnableDataProcessingBackgroundService(options =>
		{
			options.PollingInterval = TimeSpan.FromSeconds(15);
			options.DrainTimeoutSeconds = 60;
			options.UnhealthyThreshold = 5;
		});

		// Assert -- verify options are configured by building the provider
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<DataProcessingHostedServiceOptions>>().Value;
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(15));
		options.DrainTimeoutSeconds.ShouldBe(60);
		options.UnhealthyThreshold.ShouldBe(5);
	}

	[Fact]
	public void UseDefaultOptions_WhenNoConfigureProvided()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.EnableDataProcessingBackgroundService();

		// Assert
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<DataProcessingHostedServiceOptions>>().Value;
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
		options.Enabled.ShouldBeTrue();
		options.DrainTimeoutSeconds.ShouldBe(30);
	}

	[Fact]
	public void BindOptionsFromConfigurationSection()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["DataProcessingService:PollingInterval"] = "00:00:15",
				["DataProcessingService:Enabled"] = "true",
				["DataProcessingService:DrainTimeoutSeconds"] = "60",
				["DataProcessingService:UnhealthyThreshold"] = "5",
			})
			.Build();

		// BindConfiguration requires IConfiguration in DI
		services.AddSingleton<IConfiguration>(config);

		// Act
		services.EnableDataProcessingBackgroundService(config, "DataProcessingService");

		// Assert
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<DataProcessingHostedServiceOptions>>().Value;
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(15));
		options.DrainTimeoutSeconds.ShouldBe(60);
		options.UnhealthyThreshold.ShouldBe(5);
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void RegisterHostedService_WhenUsingConfigurationOverload()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder().Build();

		// Act
		services.EnableDataProcessingBackgroundService(config, "DataProcessingService");

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IHostedService) &&
			sd.ImplementationType == typeof(DataProcessingHostedService));
	}

	[Fact]
	public void RegisterOptionsValidator_WhenUsingConfigurationOverload()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder().Build();

		// Act
		services.EnableDataProcessingBackgroundService(config, "DataProcessingService");

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IValidateOptions<DataProcessingHostedServiceOptions>) &&
			sd.ImplementationType == typeof(DataProcessingHostedServiceOptionsValidator));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigurationIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.EnableDataProcessingBackgroundService(null!, "DataProcessingService"));
	}

	[Fact]
	public void ThrowArgumentException_WhenSectionPathIsEmpty()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder().Build();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.EnableDataProcessingBackgroundService(config, ""));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenServicesIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).EnableDataProcessingBackgroundService());
	}

	[Fact]
	public void ThrowArgumentNullException_WhenServicesIsNull_ConfigurationOverload()
	{
		// Arrange
		var config = new ConfigurationBuilder().Build();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).EnableDataProcessingBackgroundService(config, "Test"));
	}

	[Fact]
	public void ReturnSameServiceCollection_ForFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.EnableDataProcessingBackgroundService();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void NotRegisterDuplicateHostedService_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.EnableDataProcessingBackgroundService();
		services.EnableDataProcessingBackgroundService();

		// Assert -- TryAddEnumerable prevents duplicates
		var hostedServices = services.Where(sd =>
			sd.ServiceType == typeof(IHostedService) &&
			sd.ImplementationType == typeof(DataProcessingHostedService))
			.ToList();
		hostedServices.Count.ShouldBe(1);
	}
}
