// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch.QueryDsl;

using Excalibur.Data.ElasticSearch;
using Excalibur.Data.ElasticSearch.Exceptions;
using Excalibur.Data.ElasticSearch.Resilience;
using Excalibur.Data.Tests.ElasticSearch.Builders;

namespace Excalibur.Data.Tests.ElasticSearch.Resilience;

/// <summary>
/// Locks the single-wrap timeout contract: when the operation's own timeout budget elapses (as opposed to
/// the caller cancelling), the result is the operation's domain exception carrying a <see cref="TimeoutException" />
/// as its <see cref="Exception.InnerException" /> -- not another instance of the same domain exception.
/// </summary>
/// <remarks>
/// The bug this locks: a timed-out operation produced an <see cref="ElasticsearchSearchException" /> whose
/// <c>InnerException</c> was ANOTHER <see cref="ElasticsearchSearchException" /> instead of the
/// <see cref="TimeoutException" />.
/// <para>
/// This does not need real Elasticsearch. A zero-second <see cref="TimeoutOptions.SearchTimeout" /> means
/// the internal timeout <see cref="CancellationTokenSource" /> is already cancelled by the time the pipeline
/// delegate runs its own <c>ThrowIfCancellationRequested</c> checks -- BEFORE the underlying
/// <see cref="Elastic.Clients.Elasticsearch.ElasticsearchClient" /> call is ever invoked. The client is
/// therefore only ever constructed, never asked to talk to a cluster, and can point at an unreachable URI
/// exactly like the sibling real-infra test does for its own (different) scenario.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ResilientElasticsearchClientTimeoutShould
{
	[Fact]
	public async Task SurfaceAnElapsedOperationTimeoutAsASingleWrappedTimeoutException()
	{
		// Arrange -- an already-elapsed timeout budget (zero seconds) makes this deterministic: the
		// internal timeout token is cancelled before the delegate's manual cancellation checks run, so the
		// underlying client call never executes and no real cluster is needed.
		var config = new ElasticsearchConfigurationOptions
		{
			Resilience = new ElasticsearchResilienceOptions
			{
				Enabled = true,
				Retry = new ElasticSearchRetryPolicyOptions
				{
					Enabled = true,
					MaxAttempts = 2,
					BaseDelay = TimeSpan.Zero,
					MaxDelay = TimeSpan.Zero,
					UseExponentialBackoff = false,
					JitterFactor = 0,
				},
				CircuitBreaker = new CircuitBreakerOptions
				{
					Enabled = false,
					MinimumThroughput = 2,
					FailureRateThreshold = 0.5,
					SamplingDuration = TimeSpan.FromSeconds(30),
					BreakDuration = TimeSpan.FromSeconds(5),
				},
				Timeouts = new TimeoutOptions
				{
					SearchTimeout = TimeSpan.Zero,
					IndexTimeout = TimeSpan.FromSeconds(60),
					BulkTimeout = TimeSpan.FromSeconds(120),
					DeleteTimeout = TimeSpan.FromSeconds(30),
				},
			},
		};

		var pipeline = new ElasticsearchResiliencePipeline(Options.Create(config.Resilience));
		var circuitBreaker = A.Fake<IElasticsearchCircuitBreaker>();
		var logger = A.Fake<ILogger<ResilientElasticsearchClient>>();

		// Never reached: SearchAsync's internal cancellation check fires before this client's SearchAsync
		// is ever invoked. The unreachable URI documents that intent -- a real call here would be a bug.
		var client = new ElasticsearchClient(new ElasticsearchClientSettings(new Uri("http://127.0.0.1:1")));

		using var resilientClient = new ResilientElasticsearchClient(client, pipeline, circuitBreaker, Options.Create(config), logger);

		var searchRequest = new SearchRequest(Indices.Parse("test-resilience-timeout")) { Query = new MatchAllQuery() };

		// Act & Assert -- a timeout the caller did not request must surface as the operation's domain
		// exception, carrying a TimeoutException as ITS DIRECT InnerException (not another
		// ElasticsearchSearchException).
		var thrown = await Should
			.ThrowAsync<ElasticsearchSearchException>(() => resilientClient.SearchAsync<TestDocument>(searchRequest, CancellationToken.None))
			.ConfigureAwait(false);

		thrown.InnerException.ShouldBeOfType<TimeoutException>();
	}
}
