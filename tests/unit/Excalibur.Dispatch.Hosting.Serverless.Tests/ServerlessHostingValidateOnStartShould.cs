// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

/// <summary>
/// Regression lock: registering serverless hosting through a provider <c>Add*</c> method wires
/// <c>ValidateOnStart</c>, so invalid <see cref="ServerlessHostOptions"/> fail fast at host startup rather than
/// silently surfacing on first use.
/// </summary>
/// <remarks>
/// All three provider entry points (<c>AddAwsLambdaHosting</c>/<c>AddAzureFunctionsHosting</c>/
/// <c>AddGoogleCloudFunctionsHosting</c>) delegate to <c>AddServerlessHosting(Action&lt;&gt;)</c>. Before the fix that
/// overload registered the validator but never called <c>ValidateOnStart()</c> — so a misconfigured option (e.g.
/// <c>MemoryLimitMB = 0</c>, which the validator rejects) did NOT fail at startup. This lock is non-vacuous: against
/// the pre-fix wiring <c>StartAsync</c> does not throw and the test goes RED.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ServerlessHostingValidateOnStartShould : UnitTestBase
{
	[Fact]
	public async Task Fail_fast_at_startup_when_a_provider_registers_invalid_options()
	{
		using var host = new HostBuilder()
			.ConfigureServices(services =>
			{
				_ = services.AddAwsLambdaHosting();
				_ = services.Configure<ServerlessHostOptions>(o => o.MemoryLimitMB = 0); // invalid: validator requires >= 1
			})
			.Build();

		_ = await Should.ThrowAsync<OptionsValidationException>(
			async () => await host.StartAsync());
	}

	[Fact]
	public async Task Start_cleanly_when_a_provider_registers_valid_options()
	{
		// Positive control: a valid configuration starts without throwing — the lock is not always-throwing.
		using var host = new HostBuilder()
			.ConfigureServices(services =>
			{
				_ = services.AddAzureFunctionsHosting();
				_ = services.Configure<ServerlessHostOptions>(o => o.MemoryLimitMB = 512);
			})
			.Build();

		await host.StartAsync();
		await host.StopAsync();
	}

	[Theory]
	[Trait("Pattern", "Regression")]
	[InlineData(ServerlessPlatform.AwsLambda)]
	[InlineData(ServerlessPlatform.AzureFunctions)]
	[InlineData(ServerlessPlatform.GoogleCloudFunctions)]
	public async Task RejectInvalidMemoryAtStartupThroughThePublicServerlessFacade(ServerlessPlatform platform)
	{
		using var host = CreateFacadeHost(platform, 0);
		var exception = await Should.ThrowAsync<OptionsValidationException>(
			() => host.StartAsync(TestContext.Current.CancellationToken)).ConfigureAwait(false);
		exception.Failures.ShouldContain(failure => failure.Contains(nameof(ServerlessHostOptions.MemoryLimitMB), StringComparison.Ordinal));
	}

	[Theory]
	[Trait("Pattern", "Regression")]
	[InlineData(ServerlessPlatform.AwsLambda)]
	[InlineData(ServerlessPlatform.AzureFunctions)]
	[InlineData(ServerlessPlatform.GoogleCloudFunctions)]
	public async Task StartHealthyFacadeAndResolveItsServicesWithoutStartingMessaging(ServerlessPlatform platform)
	{
		using var host = CreateFacadeHost(platform, 128);
		await host.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		var options = host.Services.GetRequiredService<IOptions<ServerlessHostOptions>>().Value;
		options.MemoryLimitMB.ShouldBe(128);
		options.ExecutionTimeout.ShouldBeNull();
		host.Services.GetRequiredService<IServerlessHostProvider>().Platform.ShouldBe(platform);
		host.Services.GetRequiredService<IColdStartOptimizer>().ShouldNotBeNull();
		host.Services.GetService<Excalibur.Dispatch.IDispatcher>().ShouldBeNull();
		host.Services.GetServices<IHostedService>().ShouldBeEmpty();
		await host.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
	}

	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task PreserveAConsumerScopedProviderWithoutCapturingItInASingletonFactory()
	{
		using var host = new HostBuilder()
			.UseDefaultServiceProvider(options =>
			{
				options.ValidateOnBuild = true;
				options.ValidateScopes = true;
			})
			.ConfigureServices(services =>
			{
				_ = services.AddLogging();
				_ = services.AddScoped<IServerlessHostProvider>(_ => A.Fake<IServerlessHostProvider>());
				_ = services.AddAzureFunctionsServerless(options => options.MemoryLimitMB = 128);
			})
			.Build();
		await host.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
		using var firstScope = host.Services.CreateScope();
		using var secondScope = host.Services.CreateScope();
		var first = firstScope.ServiceProvider.GetRequiredService<IServerlessHostProvider>();
		firstScope.ServiceProvider.GetRequiredService<IServerlessHostProvider>().ShouldBeSameAs(first);
		secondScope.ServiceProvider.GetRequiredService<IServerlessHostProvider>().ShouldNotBeSameAs(first);
		host.Services.GetService<IServerlessHostProviderFactory>().ShouldBeNull();
		await host.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
	}

	private static IHost CreateFacadeHost(ServerlessPlatform platform, int memoryLimit)
	{
		return new HostBuilder()
			.ConfigureServices(services =>
			{
				_ = services.AddLogging();
				switch (platform)
				{
					case ServerlessPlatform.AwsLambda:
						_ = services.AddAwsLambdaServerless(options => options.MemoryLimitMB = memoryLimit);
						break;
					case ServerlessPlatform.AzureFunctions:
						_ = services.AddAzureFunctionsServerless(options => options.MemoryLimitMB = memoryLimit);
						break;
					case ServerlessPlatform.GoogleCloudFunctions:
						_ = services.AddGoogleCloudFunctionsServerless(options => options.MemoryLimitMB = memoryLimit);
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(platform), platform, null);
				}
			})
			.Build();
	}
}
