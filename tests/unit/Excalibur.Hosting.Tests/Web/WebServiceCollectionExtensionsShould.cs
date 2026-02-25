// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Web.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using ExcaliburProblemDetailsOptions = Excalibur.Hosting.Web.Diagnostics.ProblemDetailsOptions;

namespace Excalibur.Hosting.Tests.Web;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class WebServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddGlobalExceptionHandlerWithDefaults()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddGlobalExceptionHandler();

		// Assert
		result.ShouldBeSameAs(services);
		services.ShouldContain(sd => sd.ServiceType.Name.Contains("IExceptionHandler"));
	}

	[Fact]
	public void AddGlobalExceptionHandlerWithCustomOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddGlobalExceptionHandler(options =>
		{
			options.StatusTypeBaseUrl = "https://custom.example.com";
		});

		// Assert — verify the options are configured by resolving from DI
		var provider = services.BuildServiceProvider();
		var resolved = provider.GetRequiredService<IOptions<ExcaliburProblemDetailsOptions>>();
		resolved.Value.StatusTypeBaseUrl.ShouldBe("https://custom.example.com");
	}

	[Fact]
	public void AddExcaliburWebServicesRegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuration = new ConfigurationBuilder().Build();

		// Act
		services.AddExcaliburWebServices(configuration, typeof(WebServiceCollectionExtensionsShould).Assembly);

		// Assert
		services.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void ThrowWhenServicesIsNullForWebServices()
	{
		// Arrange
		var configuration = new ConfigurationBuilder().Build();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddExcaliburWebServices(configuration));
	}

	[Fact]
	public void ThrowWhenConfigurationIsNullForWebServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburWebServices(null!));
	}
}
