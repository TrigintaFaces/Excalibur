// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AwsLambda;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsLambdaServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddAwsLambdaServerless_ThrowOnNullServices()
	{
		Should.Throw<ArgumentNullException>(() => AwsLambdaServiceCollectionExtensions.AddAwsLambdaServerless(null!));
	}

	[Fact]
	public void AddAwsLambdaServerless_RegisterExpectedServices()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<ILogger>(NullLogger.Instance);

		var result = services.AddAwsLambdaServerless();

		result.ShouldBeSameAs(services);
		services.ShouldContain(sd => sd.ServiceType == typeof(IServerlessHostProvider));
		services.ShouldContain(sd => sd.ServiceType == typeof(IColdStartOptimizer));
		services.ShouldContain(sd => sd.ServiceType == typeof(DefaultLambdaJsonSerializer));
	}

	[Fact]
	public void AddAwsLambdaServerless_WithOptions_ThrowOnNullArgs()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() => AwsLambdaServiceCollectionExtensions.AddAwsLambdaServerless(null!, _ => { }));
		Should.Throw<ArgumentNullException>(() => services.AddAwsLambdaServerless(null!));
	}

	[Fact]
	public void AddAwsLambdaServerless_WithOptions_ConfigureOptionsAndRemainIdempotent()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<ILogger>(NullLogger.Instance);

		_ = services.AddAwsLambdaServerless(options => options.EnableDistributedTracing = false);
		_ = services.AddAwsLambdaServerless();

		var hostProviderRegistrations = services.Count(sd => sd.ServiceType == typeof(IServerlessHostProvider));
		hostProviderRegistrations.ShouldBe(1);

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<ServerlessHostOptions>>().Value;
		options.EnableDistributedTracing.ShouldBeFalse();
	}
}
