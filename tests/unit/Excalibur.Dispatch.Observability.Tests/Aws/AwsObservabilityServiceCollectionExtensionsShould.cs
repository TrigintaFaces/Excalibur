// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Suppress for test

using Excalibur.Dispatch.Observability.Aws;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Tests.Aws;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsObservabilityServiceCollectionExtensionsShould
{
	[Fact]
	public void RegisterIAwsTracingIntegration()
	{
		var services = new ServiceCollection();
		services.AddAwsObservability(options => options.ServiceName = "test");

		services.ShouldContain(s => s.ServiceType == typeof(IAwsTracingIntegration));
	}

	[Fact]
	public void RegisterIAwsTracingIntegrationAsSingleton()
	{
		var services = new ServiceCollection();
		services.AddAwsObservability(options => options.ServiceName = "test");

		var descriptor = services.First(s => s.ServiceType == typeof(IAwsTracingIntegration));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void RegisterImplementationType()
	{
		var services = new ServiceCollection();
		services.AddAwsObservability(options => options.ServiceName = "test");

		var descriptor = services.First(s => s.ServiceType == typeof(IAwsTracingIntegration));
		descriptor.ImplementationType.ShouldBe(typeof(AwsTracingIntegration));
	}

	[Fact]
	public void RegisterOptions()
	{
		var services = new ServiceCollection();
		services.AddAwsObservability(options =>
		{
			options.ServiceName = "my-service";
			options.Region = "us-west-2";
		});

		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsObservabilityOptions>>();
		options.Value.ServiceName.ShouldBe("my-service");
		options.Value.Region.ShouldBe("us-west-2");
	}

	[Fact]
	public void ThrowOnNullServices()
	{
		IServiceCollection services = null!;
		Should.Throw<ArgumentNullException>(() =>
			services.AddAwsObservability(options => options.ServiceName = "test"));
	}

	[Fact]
	public void ThrowOnNullConfigure()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddAwsObservability(null!));
	}

	[Fact]
	public void ReturnServiceCollectionForChaining()
	{
		var services = new ServiceCollection();
		var result = services.AddAwsObservability(options => options.ServiceName = "test");
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void ResolveAwsTracingIntegration()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddAwsObservability(options => options.ServiceName = "test");

		using var provider = services.BuildServiceProvider();
		var integration = provider.GetRequiredService<IAwsTracingIntegration>();
		integration.ShouldNotBeNull();
		integration.ShouldBeOfType<AwsTracingIntegration>();
	}
}

#pragma warning restore IL2026, IL3050
