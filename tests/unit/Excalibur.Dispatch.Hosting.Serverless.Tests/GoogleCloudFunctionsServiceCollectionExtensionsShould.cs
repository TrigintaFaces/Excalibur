// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.GoogleCloud;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class GoogleCloudFunctionsServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddGoogleCloudFunctionsServerless_ThrowOnNullServices()
	{
		Should.Throw<ArgumentNullException>(() => GoogleCloudFunctionsServiceCollectionExtensions.AddGoogleCloudFunctionsServerless(null!));
	}

	[Fact]
	public void AddGoogleCloudFunctionsServerless_RegisterExpectedServices()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<ILogger>(NullLogger.Instance);

		var result = services.AddGoogleCloudFunctionsServerless();

		result.ShouldBeSameAs(services);
		services.ShouldContain(sd => sd.ServiceType == typeof(IServerlessHostProvider));
		services.ShouldContain(sd => sd.ServiceType == typeof(IColdStartOptimizer));
	}

	[Fact]
	public void AddGoogleCloudFunctionsServerless_WithOptions_ThrowOnNullArgs()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() => GoogleCloudFunctionsServiceCollectionExtensions.AddGoogleCloudFunctionsServerless(null!, _ => { }));
		Should.Throw<ArgumentNullException>(() => services.AddGoogleCloudFunctionsServerless(null!));
	}

	[Fact]
	public void AddGoogleCloudFunctionsServerless_WithOptions_ConfigureOptionsAndRemainIdempotent()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<ILogger>(NullLogger.Instance);

		_ = services.AddGoogleCloudFunctionsServerless(options => options.EnableColdStartOptimization = false);
		_ = services.AddGoogleCloudFunctionsServerless();

		var hostProviderRegistrations = services.Count(sd => sd.ServiceType == typeof(IServerlessHostProvider));
		hostProviderRegistrations.ShouldBe(1);

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<ServerlessHostOptions>>().Value;
		options.EnableColdStartOptimization.ShouldBeFalse();
	}
}
