// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.Hosting;

namespace Excalibur.Data.Tests.ElasticSearch.Security;

/// <summary>
/// Binds the documented Elasticsearch security entry point to the thing a host actually does at
/// startup: resolve every registered hosted service. A registration that only builds a container
/// proves nothing, because every dependency in this composition is resolved lazily.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ElasticsearchSecurityHostStartupShould
{
	private static IConfiguration BuildConfiguration() =>
		new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
			{
				["Elasticsearch:Security:Encryption:KeyManagement:Provider"] = "Local",
				["Elasticsearch:Security:Monitoring:FailedLoginThreshold"] = "11",
				["Elasticsearch:Security:Monitoring:MonitoringInterval"] = "00:02:00",
			})
			.Build();

	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(_ => new ElasticsearchClient());

		// The two-argument form: the entry point that wires the full security stack.
		_ = services.AddElasticsearchSecurity(BuildConfiguration(), static _ => { });

		// Deliberately not ValidateOnBuild: this test asserts the startup path (hosted services and
		// the monitor they reach), and a whole-container validation would also fail on registrations
		// no host resolves at start, which are tracked separately.
		return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
	}

	[Fact]
	public void ResolveEveryHostedService_SoTheHostCanStart()
	{
		using var provider = BuildProvider();

		var hostedServices = provider.GetServices<IHostedService>().ToList();

		hostedServices.ShouldNotBeEmpty();
		hostedServices.OfType<BackgroundService>().ShouldNotBeEmpty();
	}

	[Fact]
	public void ResolveTheSecurityMonitor_WithItsMonitoringOptions()
	{
		using var provider = BuildProvider();

		var monitor = provider.GetRequiredService<IElasticsearchSecurityMonitor>();

		monitor.Configuration.ShouldNotBeNull();
	}

	/// <summary>
	/// The liveness half: monitoring options are not merely resolvable, they carry what the consumer
	/// configured. Init-only properties are unreachable to a <c>Configure</c> lambda but reachable to
	/// the configuration binder, so this fails if the registration reverts to defaults-only.
	/// </summary>
	[Fact]
	public void BindMonitoringOptionsFromTheConfiguredSection()
	{
		using var provider = BuildProvider();

		var options = provider.GetRequiredService<IOptions<SecurityMonitoringOptions>>().Value;

		options.FailedLoginThreshold.ShouldBe(11);
		options.MonitoringInterval.ShouldBe(TimeSpan.FromMinutes(2));
	}

	/// <summary>
	/// The authentication provider takes an <c>IHttpClientFactory</c> for OAuth2 token operations.
	/// Registering it while nothing in the package supplies a factory advertises a service the container
	/// cannot construct: the host only finds out when something resolves it. Nothing here calls
	/// <c>AddHttpClient</c>, so this fails unless the security entry point registers the factory itself.
	/// </summary>
	[Fact]
	public void ResolveTheAuthenticationProvider_WithoutTheHostRegisteringAnHttpClientFactory()
	{
		using var provider = BuildProvider();

		var authProvider = provider.GetRequiredService<IElasticsearchAuthenticationProvider>();

		authProvider.ShouldNotBeNull();
	}

	/// <summary>
	/// The two-argument call a consumer actually writes. There must be exactly one method it can bind to,
	/// and it must be the one that wires the whole stack from the <c>Elasticsearch:Security</c> section --
	/// not a second same-shaped overload registering a subset and binding the configuration root.
	/// </summary>
	[Fact]
	public void WireTheFullStack_FromTheTwoArgumentConfigurationCall()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(_ => new ElasticsearchClient());

		_ = services.AddElasticsearchSecurity(BuildConfiguration());

		using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

		// Wired: the monitoring, authentication and provider registrations the subset overload never made.
		provider.GetRequiredService<IElasticsearchSecurityMonitor>().ShouldNotBeNull();
		provider.GetRequiredService<IElasticsearchAuthenticationProvider>().ShouldNotBeNull();
		provider.GetRequiredService<IElasticsearchSecurityProvider>().ShouldNotBeNull();

		// Bound from the section, not the configuration root.
		provider.GetRequiredService<IOptions<SecurityMonitoringOptions>>().Value.FailedLoginThreshold.ShouldBe(11);
	}

	/// <summary>
	/// Alert generation and storage are real; delivery to a notification channel is not implemented. The
	/// result type must therefore carry no distribution-status member, because a consumer reading one
	/// would be told alerts reached somewhere they never reached.
	/// </summary>
	[Fact]
	public void NotAdvertiseADistributionStatus_OnTheAlertResult()
	{
		var members = typeof(SecurityAlertResult)
			.GetMembers()
			.Select(static m => m.Name)
			.Where(static n => n.Contains("Distribut", StringComparison.OrdinalIgnoreCase))
			.ToList();

		members.ShouldBeEmpty();
	}
}
