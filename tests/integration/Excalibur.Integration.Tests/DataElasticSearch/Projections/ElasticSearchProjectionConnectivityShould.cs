// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.ElasticSearch.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Integration.Tests.DataElasticSearch.Projections;

/// <summary>
/// The LIVENESS half of the start-up connectivity probe, against a real Elasticsearch container.
/// </summary>
/// <remarks>
/// <para>
/// WHY THIS CANNOT LIVE IN THE UNIT SUITE, and why its absence would have mattered. The unit arms
/// prove the probe REFUSES an unreachable cluster -- they point it at a dead port and require it to
/// throw. Every one of them is also satisfied by a probe that throws unconditionally. A check that
/// always fails and a check that works are indistinguishable from the safety side, and the difference
/// only shows up the first time someone starts a host against a cluster that is perfectly fine.
/// </para>
/// <para>
/// So this is the arm that says the probe does not cry wolf, and it needs a real cluster to say it.
/// Faking a successful ping in a unit test would assert that a stub returns what the stub was told to
/// return, which is why that was not done instead.
/// </para>
/// <para>
/// Never skipped: the container fixture structurally requires Docker rather than skipping, so this
/// cannot pass by not running. Run serially (<c>-m:1</c>).
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "Elasticsearch")]
[Trait("Component", "Projections")]
[Collection(nameof(Infrastructure.TestBaseClasses.ElasticsearchHostTests))]
public sealed class ElasticSearchProjectionConnectivityShould(ElasticsearchContainerFixture fixture)
{
	private sealed class OrderSummary
	{
		public string Id { get; set; } = string.Empty;
	}

	private readonly ElasticsearchContainerFixture _fixture = fixture;

	[Fact]
	public async Task AcceptAClusterThatIsActuallyRunning()
	{
		_fixture.ConnectionString.ShouldNotBeNullOrWhiteSpace(
			"the Elasticsearch container must be up -- this lock is never skipped");

		var builder = Host.CreateApplicationBuilder();
		_ = builder.Services.AddElasticSearchProjections(
			_fixture.ConnectionString,
			projections => projections.Add<OrderSummary>(o =>
			{
				o.Index.IndexName = "connectivity-probe-ordersummary";

				// The probe must answer from the PING alone. Creating an index would make this arm pass
				// for a second reason and leave an index behind for the fixture to clean up.
				o.Index.CreateIndexOnInitialize = false;
			}));

		using var host = builder.Build();

		// The whole assertion: against a cluster that is up, this completes. No throw.
		await host.VerifyElasticSearchProjectionConnectivityAsync(TestContext.Current.CancellationToken)
			.ConfigureAwait(true);
	}
}
