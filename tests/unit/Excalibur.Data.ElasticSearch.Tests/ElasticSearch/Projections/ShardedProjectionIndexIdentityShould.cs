// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.Data.ElasticSearch.Sharding;
using Excalibur.Data.Sharding;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

/// <summary>
/// The tenant-sharded store must write to the index the projection was REGISTERED with, prefixed by the
/// shard. It used to be built from a bare options object carrying only the shard's two coordinates, so
/// every registered setting was dropped — the index name included, which silently undid the one remedy
/// that keeps two same-named read models apart.
/// </summary>
/// <remarks>
/// This is the path the start-up index-identity check cannot see: it reads the registered options, so a
/// per-shard store that ignores them would collide after the check had certified the composition. A gate
/// that turns a detectable collision into a certified-safe one is worse than no gate, which is why this
/// arm exists beside it rather than later.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "Data")]
[Trait(TraitNames.Feature, TestFeatures.Projections)]
public sealed class ShardedProjectionIndexIdentityShould
{
	/// <summary>
	/// The registered index NAME survives into the shard, and the shard supplies only the prefix.
	/// </summary>
	[Fact]
	public void CarryTheRegisteredIndexNameIntoThePerShardStore()
	{
		var registered = new ElasticSearchProjectionStoreOptions
		{
			NodeUri = "http://registered:9200",
			Index = { IndexPrefix = "registered", IndexName = "opsemployee" },
		};

		var resolver = new ElasticSearchTenantProjectionStoreResolver<Ops.EmployeeProjection>(
			new OneShardMap(),
			NullLoggerFactory.Instance,
			new FixedOptions(registered));

		var store = (ElasticSearchProjectionStore<Ops.EmployeeProjection>)resolver.Resolve("tenant-1");

		// The shard's prefix, the REGISTRATION's name. Without the name the index falls back to the
		// simple type name -- "shard1-employeeprojection" -- which is the collision this prevents.
		store.ResolvedIndexName.ShouldBe("shard1-opsemployee");
	}

	/// <summary>
	/// The other registered settings survive too. They were dropped by the same mechanism, so a fix that
	/// carried only the index name would leave the rest of the class in place.
	/// </summary>
	[Fact]
	public void CarryTheOtherRegisteredSettingsIntoThePerShardStore()
	{
		var registered = new ElasticSearchProjectionStoreOptions
		{
			NodeUri = "http://registered:9200",
			RequestTimeoutSeconds = 77,
			Auth = { ApiKey = "an-api-key" },
			Index =
			{
				IndexPrefix = "registered",
				NumberOfReplicas = 3,
				RefreshInterval = "30s",
				CreateIndexOnInitialize = false,
			},
		};

		var perShard = registered.CloneForShard("http://shard-a:9200", "shard1");

		perShard.RequestTimeoutSeconds.ShouldBe(77);
		perShard.Auth.ApiKey.ShouldBe("an-api-key");
		perShard.Index.NumberOfReplicas.ShouldBe(3);
		perShard.Index.RefreshInterval.ShouldBe("30s");
		perShard.Index.CreateIndexOnInitialize.ShouldBeFalse();

		// And the shard's own coordinates replace the registered cluster.
		perShard.NodeUri.ShouldBe("http://shard-a:9200");
		perShard.Index.IndexPrefix.ShouldBe("shard1");
	}

	/// <summary>
	/// The registered cluster's node LIST must not survive, because it outranks the single node address
	/// the shard supplies — leaving it would send every shard back to the default cluster.
	/// </summary>
	[Fact]
	public void DropTheRegisteredNodeListSoTheShardAddressIsUsed()
	{
		var registered = new ElasticSearchProjectionStoreOptions
		{
			NodeUris = [new Uri("http://registered-a:9200"), new Uri("http://registered-b:9200")],
			Index = { IndexPrefix = "registered" },
		};

		var perShard = registered.CloneForShard("http://shard-a:9200", "shard1");

		perShard.NodeUris.ShouldBeNull();
		perShard.NodeUri.ShouldBe("http://shard-a:9200");
	}

	/// <summary>One shard, so the resolver has somewhere to route a tenant.</summary>
	private sealed class OneShardMap : ITenantShardMap
	{
		public ShardInfo GetShardInfo(string tenantId) =>
			new("shard1", "http://shard-a:9200", IndexPrefix: "shard1");

		public IReadOnlyCollection<string> GetRegisteredShardIds() => ["shard1"];
	}

	/// <summary>The registered named options, as the resolver reads them.</summary>
	private sealed class FixedOptions(ElasticSearchProjectionStoreOptions value)
		: IOptionsMonitor<ElasticSearchProjectionStoreOptions>
	{
		public ElasticSearchProjectionStoreOptions CurrentValue => value;

		public ElasticSearchProjectionStoreOptions Get(string? name) => value;

		public IDisposable? OnChange(Action<ElasticSearchProjectionStoreOptions, string?> listener) => null;
	}
}
