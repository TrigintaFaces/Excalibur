// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Persistence;
using Excalibur.Data.Persistence;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Persistence;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="ElasticsearchPersistenceProvider"/> using the
/// Persistence Provider Conformance Test Kit against a live Elasticsearch container.
/// </summary>
/// <remarks>
/// <para>
/// Never skipped: when Docker is unavailable the fixture fails fast, so a missing container surfaces as a
/// failure rather than a silent pass. The provider is constructed with the SDK's default-serializer
/// <see cref="ElasticsearchClient"/> pointed at the fixture's container.
/// </para>
/// <para>
/// <see cref="ElasticsearchPersistenceProvider"/> implements only
/// <see cref="IPersistenceProviderHealth"/>. It declines both
/// <see cref="IPersistenceProviderConnection"/> and <see cref="IPersistenceProviderTransaction"/>, and
/// exposes no connection string or retry policy to supply them from, so the kit arms that resolve those
/// two capabilities return without asserting. Declining is the documented answer, so that is conformant
/// rather than a failure. Only <see cref="IPersistenceProviderHealth"/> is declared in
/// <c>RequiredCapabilities</c>, which is what keeps the distinction honest: the health arms must assert,
/// and an implementation that quietly stopped offering health would fail loudly instead of joining the
/// arms that legitimately return early.
/// </para>
/// </remarks>
[Collection(ElasticsearchPersistenceProviderTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "ElasticSearch")]
[Trait("Pattern", "PROVIDER")]
public sealed class ElasticsearchPersistenceProviderConformanceShould : PersistenceProviderConformanceTestKit, IClassFixture<ElasticsearchContainerFixture>
{
	private readonly ElasticsearchContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticsearchPersistenceProviderConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Elasticsearch container fixture.</param>
	public ElasticsearchPersistenceProviderConformanceShould(ElasticsearchContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override string ExpectedProviderType => "Search";

	/// <inheritdoc/>
	/// <remarks>
	/// One specification for every deriver: the suite configures this name on the provider and
	/// expects the provider to report it back. An expectation set to whatever the provider happens
	/// to return cannot detect a provider that ignores its configured name -- it certifies it.
	/// </remarks>

	/// <inheritdoc/>
	protected override IReadOnlyCollection<Type> RequiredCapabilities => [typeof(IPersistenceProviderHealth)];

	/// <inheritdoc/>
	protected override IPersistenceProvider CreateProvider(string providerName)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Elasticsearch container must be available - real-infra conformance is never skipped.");

		var settings = new ElasticsearchClientSettings(new Uri(_fixture.ConnectionString));
		var client = new ElasticsearchClient(settings);

		var options = Options.Create(new ElasticsearchPersistenceOptions { Name = providerName });

		return new ElasticsearchPersistenceProvider(client, options, NullLogger<ElasticsearchPersistenceProvider>.Instance);
	}

	// -------------------------------------------------------------------------------------------------
	// Conformance arm wiring.
	//
	// The kit ships without test-framework attributes so that a consumer is not forced onto our runner,
	// which means discovery is this suite's job: one member per arm, attributed for xUnit. The kit's
	// ConformanceSuite_ShouldWireEveryArm arm fails if any arm here is missing, so an arm cannot be
	// silently dropped. Every arm below is wired unconditionally, including the ones that resolve the two
	// capabilities this provider declines -- those return without asserting, which is the conformant
	// answer rather than a failure (see the class remarks). Wiring them anyway keeps the declining
	// visible in the results instead of hiding it behind a skip.
	// -------------------------------------------------------------------------------------------------

	[Fact] public void Provider_ShouldHaveNonNullName_Test() => Provider_ShouldHaveNonNullName();
	[Fact] public void Provider_ShouldHaveExpectedName_Test() => Provider_ShouldHaveExpectedName();
	[Fact] public void Provider_NameShouldRoundTripEveryConfiguredName_Test() => Provider_NameShouldRoundTripEveryConfiguredName();
	[Fact] public Task Provider_NameShouldBeStableAcrossLifecycle_Test() => Provider_NameShouldBeStableAcrossLifecycle();
	[Fact] public void Provider_ShouldHaveNonNullProviderType_Test() => Provider_ShouldHaveNonNullProviderType();
	[Fact] public void Provider_ShouldHaveExpectedProviderType_Test() => Provider_ShouldHaveExpectedProviderType();
	[Fact] public void Provider_ShouldHaveNonNullConnectionString_Test() => Provider_ShouldHaveNonNullConnectionString();
	[Fact] public void Provider_ShouldHaveNonNullRetryPolicy_Test() => Provider_ShouldHaveNonNullRetryPolicy();
	[Fact] public void CreateTransactionScope_ShouldReturnNonNullScope_Test() => CreateTransactionScope_ShouldReturnNonNullScope();
	[Fact] public void CreateTransactionScope_WithIsolationLevel_ShouldReturnScope_Test() => CreateTransactionScope_WithIsolationLevel_ShouldReturnScope();
	[Fact] public void CreateTransactionScope_WithTimeout_ShouldReturnScope_Test() => CreateTransactionScope_WithTimeout_ShouldReturnScope();
	[Fact] public Task GetMetricsAsync_ShouldReturnNonNullDictionary_Test() => GetMetricsAsync_ShouldReturnNonNullDictionary();
	[Fact] public Task TestConnectionAsync_ShouldReachTheDatabase_WithoutAnExplicitInitialize_Test() => TestConnectionAsync_ShouldReachTheDatabase_WithoutAnExplicitInitialize();
	[Fact] public Task GetMetricsAsync_ShouldContainProviderKey_Test() => GetMetricsAsync_ShouldContainProviderKey();
	[Fact] public void Dispose_ShouldNotThrow_Test() => Dispose_ShouldNotThrow();
	[Fact] public void Dispose_CalledMultipleTimes_ShouldNotThrow_Test() => Dispose_CalledMultipleTimes_ShouldNotThrow();
	[Fact] public Task DisposeAsync_ShouldNotThrow_Test() => DisposeAsync_ShouldNotThrow();
	[Fact] public void IsAvailable_AfterDispose_ShouldBeFalse_Test() => IsAvailable_AfterDispose_ShouldBeFalse();
	[Fact] public void Provider_ShouldOfferRequiredCapabilities_Test() => Provider_ShouldOfferRequiredCapabilities();
	[Fact] public void Provider_ShouldImplementIDisposable_Test() => Provider_ShouldImplementIDisposable();
	[Fact] public void Provider_ShouldImplementIAsyncDisposable_Test() => Provider_ShouldImplementIAsyncDisposable();
	[Fact] public Task ExecuteBatchAsync_WhenARequestFails_ShouldLeaveNothingCommitted_Test() => ExecuteBatchAsync_WhenARequestFails_ShouldLeaveNothingCommitted();
	[Fact] public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
	[Fact] public void ConformanceSuite_ShouldDeclareEveryCapabilityTheProviderOffers_Test() => ConformanceSuite_ShouldDeclareEveryCapabilityTheProviderOffers();
}
