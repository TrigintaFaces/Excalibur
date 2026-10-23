// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.ElasticSearch.Projections;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

internal sealed class ConfigBoundProjection
{
	public string Id { get; set; } = string.Empty;
}

internal sealed class SecondConfigBoundProjection
{
	public string Id { get; set; } = string.Empty;
}

/// <summary>
/// A projection store's cluster comes from configuration, and a cluster nobody configured is refused.
/// </summary>
/// <remarks>
/// <para>
/// There was no way to bind these options from <c>IConfiguration</c>, so every consumer hand-rolled the
/// read -- and the hand-rolled version had to do something the framework would not: raise when the node
/// list came back empty. It could not simply be left to validation, because <c>NodeUri</c> defaulted to
/// <c>http://localhost:9200</c>, so a misnamed section, a missing environment variable, or a JSON object
/// where an array was meant did not fail. It ran the whole application against one unconfigured node.
/// </para>
/// <para>
/// The default is gone, which makes the required-node check that was already written in
/// <c>Validate()</c> reachable for the first time.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "ElasticSearch")]
public sealed class ProjectionStoreConfigurationBindingShould
{
	/// <summary>
	/// The arm that proves a JSON ARRAY of nodes reaches <c>List&lt;Uri&gt;</c>. Everything else here
	/// rests on that: a cluster that bound as a single node would fail over nowhere.
	/// </summary>
	[Fact]
	public void BindTheWholeClusterFromAConfigurationSection()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddElasticSearchProjections(
			Section(new Dictionary<string, string?>
			{
				["Elasticsearch:NodeUris:0"] = "http://es01:9200",
				["Elasticsearch:NodeUris:1"] = "http://es02:9200",
				["Elasticsearch:Auth:ApiKey"] = "not-a-real-key",
				["Elasticsearch:Index:IndexPrefix"] = "readmodels",
				["Elasticsearch:Index:NumberOfReplicas"] = "1",
			}),
			p => p.Add<ConfigBoundProjection>());

		var options = Options(services);

		options.NodeUris.ShouldNotBeNull();
		options.NodeUris!.Select(static uri => uri.ToString())
			.ShouldBe(["http://es01:9200/", "http://es02:9200/"]);
		options.Auth.ApiKey.ShouldBe("not-a-real-key");
		options.Index.IndexPrefix.ShouldBe("readmodels");
		options.Index.NumberOfReplicas.ShouldBe(1);
	}

	/// <summary>
	/// One section describes the cluster; each projection still owns its own index name. This is the
	/// shape a consumer with several read models on one cluster actually writes.
	/// </summary>
	[Fact]
	public void ShareOneSectionAcrossEveryProjectionWithoutSharingAnIndex()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddElasticSearchProjections(
			Section(new Dictionary<string, string?> { ["Elasticsearch:NodeUris:0"] = "http://es01:9200" }),
			p => p
				.Add<ConfigBoundProjection>(o => o.Index.IndexName = "first")
				.Add<SecondConfigBoundProjection>(o => o.Index.IndexName = "second"));

		var monitor = Monitor(services);

		monitor.Get(typeof(ConfigBoundProjection).FullName!).NodeUris.ShouldNotBeNull();
		monitor.Get(typeof(SecondConfigBoundProjection).FullName!).NodeUris.ShouldNotBeNull();
		monitor.Get(typeof(ConfigBoundProjection).FullName!).Index.IndexName.ShouldBe("first");
		monitor.Get(typeof(SecondConfigBoundProjection).FullName!).Index.IndexName.ShouldBe("second");
	}

	/// <summary>
	/// Code wins over configuration, which is the conventional precedence and the only one that lets a
	/// per-projection override mean anything when the shared section already set the same property.
	/// </summary>
	[Fact]
	public void LetCodeOverrideWhatConfigurationSet()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddElasticSearchProjections(
			Section(new Dictionary<string, string?>
			{
				["Elasticsearch:NodeUris:0"] = "http://es01:9200",
				["Elasticsearch:Index:IndexPrefix"] = "from-configuration",
			}),
			p => p.Add<ConfigBoundProjection>(o => o.Index.IndexPrefix = "from-code"));

		Options(services).Index.IndexPrefix.ShouldBe("from-code");
	}

	/// <summary>
	/// SAFETY: a section that names no node is refused at start-up rather than silently addressing one
	/// unconfigured node. This is the arm the removed <c>localhost:9200</c> default made unreachable.
	/// </summary>
	[Fact]
	public void RefuseToStartWhenNoClusterIsConfigured()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// The shape a misnamed section or a missing environment variable actually produces: present,
		// and empty.
		_ = services.AddElasticSearchProjections(
			Section(new Dictionary<string, string?> { ["Unrelated:Key"] = "value" }),
			p => p.Add<ConfigBoundProjection>());

		using var provider = services.BuildServiceProvider();

		var error = Should.Throw<OptionsValidationException>(
			() => provider.GetRequiredService<IStartupValidator>().Validate());

		// It must name what to set. A refusal a consumer cannot act on is a worse failure than the
		// silent default it replaced.
		error.Message.ShouldContain("NodeUris", Case.Sensitive);
	}

	/// <summary>
	/// LIVENESS. Without this the refusal above is satisfied by validation that rejects everything, and
	/// the whole change would read as working while making the store unusable.
	/// </summary>
	[Fact]
	public void StartWhenTheClusterIsConfigured()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddElasticSearchProjections(
			Section(new Dictionary<string, string?> { ["Elasticsearch:NodeUris:0"] = "http://es01:9200" }),
			p => p.Add<ConfigBoundProjection>());

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => provider.GetRequiredService<IStartupValidator>().Validate());
	}

	/// <summary>
	/// The single-projection overload binds the same way, so a consumer with one read model is not
	/// pushed into the registrar to get configuration binding.
	/// </summary>
	[Fact]
	public void BindTheSingleProjectionOverloadToo()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddElasticSearchProjectionStore<ConfigBoundProjection>(
			Section(new Dictionary<string, string?>
			{
				["Elasticsearch:NodeUris:0"] = "http://es01:9200",
				["Elasticsearch:Index:IndexPrefix"] = "readmodels",
			}));

		var options = Options(services);

		options.NodeUris.ShouldNotBeNull();
		options.Index.IndexPrefix.ShouldBe("readmodels");
	}

	/// <summary>
	/// LIVENESS, and a regression lock. When the consumer SUPPLIES the client, the node address is never
	/// read -- so requiring one refuses a correct registration.
	/// </summary>
	/// <remarks>
	/// Removing the node default made the required-node check reachable on every registration path,
	/// including this one, where eager validation was already wired. The documented client-factory
	/// example sets only an index name, so it started failing at start-up on a cluster it reaches
	/// perfectly well. The node is a FALLBACK for the paths that build a client; it is not a
	/// requirement of a path that is handed one.
	/// </remarks>
	[Fact]
	public void StartWhenTheConsumerSuppliesTheClientAndNoNodeAddress()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddElasticSearchProjectionStore<ConfigBoundProjection>(
			static _ => new ElasticsearchClient(),
			static o => o.Index.IndexName = "ordersummary");

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => provider.GetRequiredService<IStartupValidator>().Validate());
	}

	private static IConfigurationSection Section(Dictionary<string, string?> values) =>
		new ConfigurationBuilder().AddInMemoryCollection(values).Build().GetSection("Elasticsearch");

	private static IOptionsMonitor<ElasticSearchProjectionStoreOptions> Monitor(IServiceCollection services) =>
		services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();

	private static ElasticSearchProjectionStoreOptions Options(IServiceCollection services) =>
		Monitor(services).Get(typeof(ConfigBoundProjection).FullName!);
}
