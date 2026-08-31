// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Data.Spanner.Tests;

/// <summary>
/// Locks for <c>AddSpannerDataProvider</c>. Every arm resolves through a real container — and the fail-fast
/// arms through a real host — because "the descriptor was added" and "the consumer can resolve it" are
/// different claims, and only the second one is what a consumer experiences.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SpannerServiceCollectionExtensionsShould
{
	private static void ConfigureValid(SpannerOptions options)
	{
		options.ProjectId = "excalibur-project";
		options.InstanceId = "excalibur-instance";
		options.DatabaseId = "excalibur-database";
	}

	[Fact]
	public void ResolveTheConnectionProvider_FromARealContainer()
	{
		var services = new ServiceCollection();

		_ = services.AddSpannerDataProvider(ConfigureValid);

		using var provider = services.BuildServiceProvider();
		var connectionProvider = provider.GetRequiredService<ISpannerConnectionProvider>();

		connectionProvider.ShouldNotBeNull();
	}

	[Fact]
	public void ResolveTheConfiguredOptions_FromARealContainer()
	{
		var services = new ServiceCollection();

		_ = services.AddSpannerDataProvider(ConfigureValid);

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SpannerOptions>>().Value;

		options.DatabasePath.ShouldBe("projects/excalibur-project/instances/excalibur-instance/databases/excalibur-database");
	}

	/// <summary>
	/// The safety half of the fail-fast contract, exercised through a real host so that
	/// <c>ValidateOnStart</c> actually runs. Resolving the options directly would not prove this: the point
	/// of <c>ValidateOnStart</c> is that a misconfigured provider is rejected before the first request, not
	/// at the first resolve.
	/// </summary>
	[Fact]
	public async Task RefuseToStartTheHost_WhenTheConfigurationIsIncomplete()
	{
		using var host = new HostBuilder()
			.ConfigureServices(services => services.AddSpannerDataProvider(options => options.ProjectId = "only-the-project"))
			.Build();

		var failure = await Should.ThrowAsync<OptionsValidationException>(
			async () => await host.StartAsync(TestContext.Current.CancellationToken));

		failure.Failures.ShouldContain(f => f.Contains(nameof(SpannerOptions.InstanceId), StringComparison.Ordinal));
		failure.Failures.ShouldContain(f => f.Contains(nameof(SpannerOptions.DatabaseId), StringComparison.Ordinal));
	}

	/// <summary>
	/// The liveness half. Without it, a registration that rejected every configuration — or a validator
	/// wired to the wrong options type and therefore never satisfied — would pass the arm above and take
	/// the whole provider down with it.
	/// </summary>
	[Fact]
	public async Task StartTheHost_WhenTheConfigurationIsComplete()
	{
		using var host = new HostBuilder()
			.ConfigureServices(services => services.AddSpannerDataProvider(ConfigureValid))
			.Build();

		await host.StartAsync(TestContext.Current.CancellationToken);
		await host.StopAsync(TestContext.Current.CancellationToken);
	}

	/// <summary>
	/// The registration uses <c>TryAddSingleton</c>, which is the framework's contract for "a consumer may
	/// override this". A consumer who has registered their own provider — to add tracing, or to pin a
	/// session pool — must keep it.
	/// </summary>
	[Fact]
	public void PreserveAConsumerSuppliedConnectionProvider()
	{
		var services = new ServiceCollection();
		var consumerProvider = new StubConnectionProvider();

		_ = services.AddSingleton<ISpannerConnectionProvider>(consumerProvider);
		_ = services.AddSpannerDataProvider(ConfigureValid);

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<ISpannerConnectionProvider>().ShouldBeSameAs(consumerProvider);
	}

	/// <summary>
	/// Registering twice is ordinary in a composed application — two stores each calling the shared
	/// registration. It must not produce two validators, because a duplicated validator turns one
	/// configuration mistake into two identical failure messages.
	/// </summary>
	[Fact]
	public void RegisterExactlyOneValidator_WhenCalledTwice()
	{
		var services = new ServiceCollection();

		_ = services.AddSpannerDataProvider(ConfigureValid);
		_ = services.AddSpannerDataProvider(ConfigureValid);

		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<SpannerOptions>>()
			.OfType<SpannerOptionsValidator>()
			.ToList();

		validators.Count.ShouldBe(1);
	}

	[Fact]
	public void Reject_ANullServiceCollection()
		=> Should.Throw<ArgumentNullException>(
			() => SpannerServiceCollectionExtensions.AddSpannerDataProvider(null!, ConfigureValid));

	[Fact]
	public void Reject_ANullConfigureDelegate()
		=> Should.Throw<ArgumentNullException>(
			() => new ServiceCollection().AddSpannerDataProvider(null!));

	private sealed class StubConnectionProvider : ISpannerConnectionProvider
	{
		public Google.Cloud.Spanner.Data.SpannerConnection CreateConnection()
			=> throw new NotSupportedException("This stub exists to be resolved, not to connect.");

		public Task<T> ExecuteInRetryableTransactionAsync<T>(
			Func<Google.Cloud.Spanner.Data.SpannerConnection, CancellationToken, Task<T>> operation,
			CancellationToken cancellationToken)
			=> throw new NotSupportedException("This stub exists to be resolved, not to connect.");
	}
}
