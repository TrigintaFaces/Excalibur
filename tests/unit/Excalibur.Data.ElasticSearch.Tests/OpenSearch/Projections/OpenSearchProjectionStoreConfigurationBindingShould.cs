// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.OpenSearch.Projections;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using OpenSearch.Client;

namespace Excalibur.Data.ElasticSearch.Tests.OpenSearch.Projections;

internal sealed class OpenSearchConfigBoundProjection
{
	public string Id { get; set; } = string.Empty;
}

internal sealed class SecondOpenSearchConfigBoundProjection
{
	public string Id { get; set; } = string.Empty;
}

/// <summary>
/// An OpenSearch projection store's cluster comes from configuration, and a cluster nobody configured is
/// refused at start-up.
/// </summary>
/// <remarks>
/// <para>
/// The Elasticsearch side of this gained configuration binding and eager validation; the OpenSearch side
/// did not, which left the two providers asymmetric for the same feature. These arms are the OpenSearch
/// half, deliberately mirroring the Elasticsearch ones so a reader can diff them.
/// </para>
/// <para>
/// Two things were missing, not one. Binding was absent, so every consumer hand-rolled the read. And the
/// cross-property validator was registered without <c>ValidateOnStart</c>, so even once the node check
/// became reachable it fired on the store's first use -- which for an async projection is inside a
/// background host, not at start-up where a configuration mistake belongs.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "OpenSearch")]
public sealed class OpenSearchProjectionStoreConfigurationBindingShould
{
	/// <summary>
	/// The arm that proves a JSON ARRAY of nodes reaches <c>List&lt;Uri&gt;</c>. Everything else rests on
	/// it: a cluster that bound as a single node would fail over nowhere.
	/// </summary>
	[Fact]
	public void BindTheWholeClusterFromAConfigurationSection()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddOpenSearchProjections(
			Section(new Dictionary<string, string?>
			{
				["OpenSearch:NodeUris:0"] = "http://os01:9200",
				["OpenSearch:NodeUris:1"] = "http://os02:9200",
				["OpenSearch:IndexPrefix"] = "readmodels",
				["OpenSearch:NumberOfReplicas"] = "1",
			}),
			p => p.Add<OpenSearchConfigBoundProjection>());

		var options = Options(services);

		options.NodeUris.ShouldNotBeNull();
		options.NodeUris!.Select(static uri => uri.ToString())
			.ShouldBe(["http://os01:9200/", "http://os02:9200/"]);
		options.IndexPrefix.ShouldBe("readmodels");
		options.NumberOfReplicas.ShouldBe(1);
	}

	/// <summary>
	/// One section describes the cluster; each projection still owns its own index name.
	/// </summary>
	[Fact]
	public void ShareOneSectionAcrossEveryProjectionWithoutSharingAnIndex()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddOpenSearchProjections(
			Section(new Dictionary<string, string?> { ["OpenSearch:NodeUris:0"] = "http://os01:9200" }),
			p => p
				.Add<OpenSearchConfigBoundProjection>(o => o.IndexName = "first")
				.Add<SecondOpenSearchConfigBoundProjection>(o => o.IndexName = "second"));

		var monitor = Monitor(services);

		monitor.Get(nameof(OpenSearchConfigBoundProjection)).NodeUris.ShouldNotBeNull();
		monitor.Get(nameof(SecondOpenSearchConfigBoundProjection)).NodeUris.ShouldNotBeNull();
		monitor.Get(nameof(OpenSearchConfigBoundProjection)).IndexName.ShouldBe("first");
		monitor.Get(nameof(SecondOpenSearchConfigBoundProjection)).IndexName.ShouldBe("second");
	}

	/// <summary>
	/// Code wins over configuration -- the conventional precedence, and the only one that lets a
	/// per-projection override mean anything when the shared section set the same property.
	/// </summary>
	[Fact]
	public void LetCodeOverrideWhatConfigurationSet()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddOpenSearchProjections(
			Section(new Dictionary<string, string?>
			{
				["OpenSearch:NodeUris:0"] = "http://os01:9200",
				["OpenSearch:IndexPrefix"] = "from-configuration",
			}),
			p => p.Add<OpenSearchConfigBoundProjection>(o => o.IndexPrefix = "from-code"));

		Options(services).IndexPrefix.ShouldBe("from-code");
	}

	/// <summary>
	/// SAFETY: a section naming no node is refused at START-UP, not at the store's first use. Both halves
	/// of that sentence are load-bearing -- the validator existed before this and ran lazily.
	/// </summary>
	[Fact]
	public void RefuseToStartWhenNoClusterIsConfigured()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// The shape a misnamed section or a missing environment variable actually produces: present,
		// and empty.
		_ = services.AddOpenSearchProjections(
			Section(new Dictionary<string, string?> { ["Unrelated:Key"] = "value" }),
			p => p.Add<OpenSearchConfigBoundProjection>());

		using var provider = services.BuildServiceProvider();

		var error = Should.Throw<OptionsValidationException>(
			() => provider.GetRequiredService<IStartupValidator>().Validate());

		// It must name what to set. A refusal a consumer cannot act on is worse than the silent default
		// it replaced.
		error.Message.ShouldContain("NodeUri", Case.Sensitive);
	}

	/// <summary>
	/// LIVENESS. Without this the refusal above is satisfied by validation that rejects everything, and
	/// the change would read as working while making the store unusable.
	/// </summary>
	[Fact]
	public void StartWhenTheClusterIsConfigured()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddOpenSearchProjections(
			Section(new Dictionary<string, string?> { ["OpenSearch:NodeUris:0"] = "http://os01:9200" }),
			p => p.Add<OpenSearchConfigBoundProjection>());

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => provider.GetRequiredService<IStartupValidator>().Validate());
	}

	/// <summary>
	/// LIVENESS for the eager wiring on the paths that do NOT bind configuration: the node-uri overload
	/// must still start. Without this arm, adding ValidateOnStart could have broken every existing
	/// registration shape and only the configuration arms would have noticed.
	/// </summary>
	[Fact]
	public void StartWhenTheClusterCameFromTheNodeUriOverload()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddOpenSearchProjectionStore<OpenSearchConfigBoundProjection>("http://os01:9200");

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

		_ = services.AddOpenSearchProjectionStore<OpenSearchConfigBoundProjection>(
			Section(new Dictionary<string, string?>
			{
				["OpenSearch:NodeUris:0"] = "http://os01:9200",
				["OpenSearch:IndexPrefix"] = "readmodels",
			}));

		var options = Options(services);

		options.NodeUris.ShouldNotBeNull();
		options.IndexPrefix.ShouldBe("readmodels");
	}

	/// <summary>
	/// LIVENESS. When the consumer SUPPLIES the client, the node address is never read -- so requiring
	/// one would refuse a correct registration. Wiring eager validation without this arm would have
	/// broken the documented client-factory example, which sets only an index name.
	/// </summary>
	[Fact]
	public void StartWhenTheConsumerSuppliesTheClientAndNoNodeAddress()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddOpenSearchProjectionStore<OpenSearchConfigBoundProjection>(
			static _ => new OpenSearchClient(),
			static o => o.IndexName = "ordersummary");

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => provider.GetRequiredService<IStartupValidator>().Validate());
	}

	private static IConfigurationSection Section(Dictionary<string, string?> values) =>
		new ConfigurationBuilder().AddInMemoryCollection(values).Build().GetSection("OpenSearch");

	private static IOptionsMonitor<OpenSearchProjectionStoreOptions> Monitor(IServiceCollection services) =>
		services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<OpenSearchProjectionStoreOptions>>();

	private static OpenSearchProjectionStoreOptions Options(IServiceCollection services) =>
		Monitor(services).Get(nameof(OpenSearchConfigBoundProjection));
}
