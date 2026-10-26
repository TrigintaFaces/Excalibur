// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Fails start-up when two projection types resolve to the same Elasticsearch index, instead of
/// letting them overwrite each other's documents in production.
/// </summary>
/// <remarks>
/// <para>
/// WHAT THIS EXISTS TO PREVENT. An index name defaults to the projection's SIMPLE type name, so two
/// read models with the same simple name in different namespaces resolve to one index. Their options
/// buckets do not collide -- those are keyed on the full name -- so nothing else in the registration
/// path notices. Both folds then write to one index, each overwriting the other wherever their
/// document ids coincide, and the result is a read model that is silently wrong rather than one that
/// fails: no exception, no log line, and two screens quietly disagreeing about the same entity.
/// </para>
/// <para>
/// WHY A REFUSAL RATHER THAN A QUALIFIED DEFAULT. Deriving the default from the full type name would
/// make the collision impossible, but it would also name every index after a namespace -- long, tied
/// to the C# layout, and renamed by a refactoring that moves a class. The short name is the right
/// default for the overwhelming majority of consumers, who have one type per read model. So the
/// default stays readable and the rare collision is refused by name, which is the same trade
/// Entity Framework Core makes for table names: the short entity name by default, and a hard failure
/// at model build naming both entities when two of them map to one table.
/// </para>
/// <para>
/// IT RUNS WHERE THE WHOLE REGISTRATION SET IS KNOWN. A single projection's registration cannot see
/// its future sibling, and the resolved index depends on options that configuration and per-projection
/// callbacks have not finished supplying at that point. Both are settled once the container is built,
/// which is why this is a start-up check over every claimed projection rather than a check inside
/// <c>AddElasticSearchProjectionStore</c>.
/// </para>
/// <para>
/// It reads options and performs no I/O, so it is safe to run twice and safe against a container that
/// was built but never started. A consumer composing a container by hand reaches it through
/// <c>ValidateStartupGates()</c>; a host reaches it as a hosted service at start.
/// </para>
/// </remarks>
internal sealed class ElasticSearchProjectionIndexIdentityValidator
	: IHostedService, IStartupPrerequisiteValidator
{
	private readonly ElasticSearchProjectionStoreExtensions.ProjectionOptionsNameClaims _claims;
	private readonly IOptionsMonitor<ElasticSearchProjectionStoreOptions> _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticSearchProjectionIndexIdentityValidator"/> class.
	/// </summary>
	/// <param name="claims">Which projection type owns each registered options bucket.</param>
	/// <param name="options">The monitor the resolved index name is read through.</param>
	public ElasticSearchProjectionIndexIdentityValidator(
		ElasticSearchProjectionStoreExtensions.ProjectionOptionsNameClaims claims,
		IOptionsMonitor<ElasticSearchProjectionStoreOptions> options)
	{
		_claims = claims ?? throw new ArgumentNullException(nameof(claims));
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken)
	{
		Validate();
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public void Validate()
	{
		// Keyed on the CLUSTER AND the resolved index string, because a document can only be
		// overwritten by something writing to the same index on the same cluster. Two read models
		// sharing an index name across two clusters never meet, and refusing that would be a false
		// alarm on a legitimate composition. Two types with one simple name and distinct prefixes are
		// likewise legitimate, which is why the key is the resolved name and not the type name.
		var owners = new Dictionary<(string Cluster, string Index), SortedSet<string>>();

		var clusterUnknown = false;

		foreach (var (optionsName, projectionType) in _claims.Owners)
		{
			// IStartupPrerequisiteValidator declares InvalidOperationException, and resolving named
			// options runs the consumer's own configure and validate callbacks -- which raise
			// OptionsValidationException, not an InvalidOperationException. Letting that escape would
			// break every client written against the interface's declared exception, so it is reported
			// as this contract's exception, naming the projection whose options could not be read.
			ElasticSearchProjectionStoreOptions options;
			try
			{
				options = _options.Get(optionsName);
			}
			catch (Exception ex) when (ex is not InvalidOperationException)
			{
				throw new InvalidOperationException(
					$"The options for projection '{projectionType.FullName ?? projectionType.Name}' could "
					+ "not be read, so it could not be checked for an index-name collision. Fix the "
					+ $"configuration for this projection's store. ({ex.Message})",
					ex);
			}

			// Derived exactly as ElasticSearchProjectionStore derives its own index: the simple type
			// name through the one convention function. A second expression here would let the check
			// and the store disagree, and the check would then pass on the composition that breaks.
			var index = ElasticSearchProjectionIndexConvention.GetIndexName(options, projectionType.Name);

			// Nodes only, never the credential: two stores on one cluster under different credentials
			// still overwrite each other, so folding the credential in here would separate them and
			// miss exactly the collision this exists to catch.
			var cluster = ElasticSearchProjectionClientSettings.DescribeClusterNodes(options);

			// A registration that supplies its own client keeps its cluster inside a delegate, so there
			// is no address to compare. Those group together under the empty key -- deliberately, because
			// grouping risks a refusal a consumer can act on while separating risks the silent overwrite
			// this exists to prevent. The message says so rather than implying the clusters matched.
			if (cluster.Length == 0)
			{
				clusterUnknown = true;
			}

			var key = (cluster, index);

			if (!owners.TryGetValue(key, out var claimants))
			{
				owners[key] = claimants = new SortedSet<string>(StringComparer.Ordinal);
			}

			// The full name, so the same type registered twice is one claimant rather than two.
			_ = claimants.Add(projectionType.FullName ?? projectionType.Name);
		}

		var collisions = owners
			.Where(static pair => pair.Value.Count > 1)
			.OrderBy(static pair => pair.Key.Index, StringComparer.Ordinal)
			.ThenBy(static pair => pair.Key.Cluster, StringComparer.Ordinal)
			.ToList();

		if (collisions.Count == 0)
		{
			return;
		}

		var detail = string.Join(
			Environment.NewLine,
			collisions.Select(static pair =>
				$"  index '{pair.Key.Index}'"
				+ (string.IsNullOrWhiteSpace(pair.Key.Cluster) ? string.Empty : $" on {pair.Key.Cluster}")
				+ $" is claimed by {string.Join(", ", pair.Value.Select(static n => $"'{n}'"))}"));

		// The example is spelled out rather than described. A consumer reading this has an index name to
		// choose and a deployment to unblock; naming a property path leaves them to work out which
		// lambda it goes in, and the per-projection overload is the only place it can go.
		var example = collisions[0].Value.Last();

		throw new InvalidOperationException(
			$"{collisions.Count} Elasticsearch index name(s) are claimed by more than one projection:"
			+ $"{Environment.NewLine}{detail}{Environment.NewLine}"
			+ "Each of these projections folds into the index named above, so they would overwrite one "
			+ "another wherever their document ids coincide, with no error reported. Unless you set it, "
			+ "an index name defaults to the projection's simple type name, which these types share."
			+ $"{Environment.NewLine}{Environment.NewLine}"
			+ "Give at least one of them its own index. Set it PER PROJECTION -- a shared configuration "
			+ "section binds the same index name into every projection and will not separate them:"
			+ $"{Environment.NewLine}"
			+ $"    projections.Add<{example}>(o => o.Index.IndexName = \"...\");"
			+ $"{Environment.NewLine}{Environment.NewLine}"
			+ "Index.IndexPrefix separates them instead, and if they are in fact the same read model, "
			+ "register only one of them."
			+ $"{Environment.NewLine}{Environment.NewLine}"
			+ "IF THIS CONFIGURATION HAS ALREADY RUN, the index above already holds documents from both "
			+ "read models, interleaved. Renaming fixes the next write, not the data: rebuild each "
			+ "projection into its new index before serving reads from it."
			+ (clusterUnknown
				? Environment.NewLine + Environment.NewLine
					+ "One or more of these registrations supplies its own Elasticsearch client, so its "
					+ "cluster address is not visible here and it was compared as though it shared a "
					+ "cluster with the others. If they address different clusters they cannot in fact "
					+ "collide -- give them distinct index names anyway, or configure node addresses "
					+ "instead of supplying a client, so this check can tell them apart."
				: string.Empty));
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
