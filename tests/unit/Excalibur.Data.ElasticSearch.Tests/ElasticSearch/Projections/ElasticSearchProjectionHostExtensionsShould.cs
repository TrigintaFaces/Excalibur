// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.ElasticSearch.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

/// <summary>
/// Unit tests for the start-up connectivity probe over registered projection stores.
/// </summary>
/// <remarks>
/// <para>
/// WHAT THESE ARMS CAN AND CANNOT ESTABLISH, stated because the asymmetry is the whole difficulty of
/// testing a reachability check. The UNREACHABLE arm is real: it addresses a port nothing listens on,
/// so the transport genuinely fails and the probe genuinely has to report it. The REACHABLE arm needs
/// a live cluster and therefore lives in the integration suite, not here -- a unit test that faked a
/// successful ping would be asserting that a stub returns what the stub was told to return.
/// </para>
/// <para>
/// So the arm that would catch the defect this probe exists to prevent is the one running here, and
/// the arm that proves it does not cry wolf is the one that is not. That is the honest split, and it
/// is stated rather than papered over.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "Data")]
[Trait(TraitNames.Feature, TestFeatures.Projections)]
public sealed class ElasticSearchProjectionHostExtensionsShould
{
	private sealed class OrderSummary
	{
		public string Id { get; set; } = string.Empty;
	}

	private sealed class CustomerProfile
	{
		public string Id { get; set; } = string.Empty;
	}

	/// <summary>A port on the loopback interface that nothing is listening on.</summary>
	/// <remarks>
	/// Port 1 is reserved and unbindable by an unprivileged process, so this refuses the connection
	/// immediately and deterministically rather than hanging until a timeout. The probe must surface
	/// that as unreachable.
	/// </remarks>
	private const string DeadNode = "http://127.0.0.1:1";

	private static IHost BuildHost(Action<IServiceCollection> configure)
	{
		var builder = Host.CreateApplicationBuilder();
		configure(builder.Services);
		return builder.Build();
	}

	[Fact]
	public async Task ThrowWhenAConfiguredClusterDoesNotAnswer()
	{
		using var host = BuildHost(services => services.AddElasticSearchProjections(
			shared => shared.NodeUri = DeadNode,
			projections => projections.Add<OrderSummary>(o => o.Index.IndexName = "ordersummary")));

		var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
			() => host.VerifyElasticSearchProjectionConnectivityAsync(TestContext.Current.CancellationToken));

		// The message has to carry BOTH halves or it does not save anyone time: which cluster, and
		// which read models stop working because of it.
		Assert.Contains("127.0.0.1:1", thrown.Message, StringComparison.Ordinal);
		Assert.Contains(nameof(OrderSummary), thrown.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task NameEveryProjectionSharingTheUnreachableCluster()
	{
		using var host = BuildHost(services => services.AddElasticSearchProjections(
			shared => shared.NodeUri = DeadNode,
			projections => projections
				.Add<OrderSummary>(o => o.Index.IndexName = "ordersummary")
				.Add<CustomerProfile>(o => o.Index.IndexName = "customerprofile")));

		var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
			() => host.VerifyElasticSearchProjectionConnectivityAsync(TestContext.Current.CancellationToken));

		Assert.Contains(nameof(OrderSummary), thrown.Message, StringComparison.Ordinal);
		Assert.Contains(nameof(CustomerProfile), thrown.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task RefuseAHostWithNoProjectionStoreRatherThanReportSuccess()
	{
		// A probe that finds nothing to probe has verified nothing. Returning quietly here would give
		// a host that registered no stores at all the same clean start-up as a host whose cluster
		// answered -- which is precisely the silence this probe was added to remove.
		using var host = BuildHost(static _ => { });

		var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
			() => host.VerifyElasticSearchProjectionConnectivityAsync(TestContext.Current.CancellationToken));

		Assert.Contains("No Elasticsearch projection store is registered", thrown.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task ThrowOnANullHost()
	{
		IHost? host = null;

		_ = await Assert.ThrowsAsync<ArgumentNullException>(
			() => host!.VerifyElasticSearchProjectionConnectivityAsync(TestContext.Current.CancellationToken));
	}

	[Fact]
	public void DescribeTwoCredentialsOnTheSameNodesAsTwoDifferentClusters()
	{
		// Reachability is not a property of an address alone. An anonymous probe can answer while the
		// store's own API key is rejected, so grouping purely by node address would let one projection's
		// success vouch for another's failure.
		var anonymous = new ElasticSearchProjectionStoreOptions { NodeUri = DeadNode };
		var keyed = new ElasticSearchProjectionStoreOptions { NodeUri = DeadNode };
		keyed.Auth.ApiKey = "an-api-key";

		Assert.NotEqual(
			ElasticSearchProjectionClientSettings.DescribeCluster(anonymous),
			ElasticSearchProjectionClientSettings.DescribeCluster(keyed));
	}

	[Fact]
	public void NeverPutACredentialIntoTheClusterDescription()
	{
		// The description reaches an exception message and a log line, so a secret in it would be
		// copied into both by a consumer reporting the failure.
		var options = new ElasticSearchProjectionStoreOptions { NodeUri = DeadNode };
		options.Auth.ApiKey = "a-secret-value";

		var described = ElasticSearchProjectionClientSettings.DescribeCluster(options);

		Assert.DoesNotContain("a-secret-value", described, StringComparison.Ordinal);
		Assert.Contains("api-key", described, StringComparison.Ordinal);
	}
}
