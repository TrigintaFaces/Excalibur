// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.OpenSearch.Persistence;
using Excalibur.Data.Persistence;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using OpenSearch.Client;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Persistence;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="OpenSearchPersistenceProvider"/> using the
/// Persistence Provider Conformance Test Kit against a live OpenSearch container.
/// </summary>
/// <remarks>
/// <para>
/// Never skipped: when Docker is unavailable the fixture fails fast, so a missing container surfaces as a
/// failure rather than a silent pass. The provider is constructed with the SDK's default-settings
/// <see cref="OpenSearchClient"/> pointed at the fixture's container.
/// </para>
/// <para>
/// <see cref="OpenSearchPersistenceProvider"/> declines <c>IPersistenceProviderTransaction</c> — its
/// <c>GetService</c> returns <see langword="null"/> for it — which the interface documents as correct, so
/// the four transaction arms return without asserting. It offers <c>IPersistenceProviderHealth</c>, so the
/// two metrics arms and the post-disposal availability arm do assert. The
/// <c>HealthCapability_ShouldBeOffered_Test</c> arm below is the liveness half of that: it fails if the
/// provider ever stops offering health, which would otherwise turn those arms silently vacuous.
/// </para>
/// </remarks>
[Collection(OpenSearchPersistenceProviderTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "OpenSearch")]
[Trait("Pattern", "PROVIDER")]
public sealed class OpenSearchPersistenceProviderConformanceShould
	: PersistenceProviderConformanceTestKit
{
	private readonly OpenSearchPersistenceProviderContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="OpenSearchPersistenceProviderConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The OpenSearch container fixture.</param>
	public OpenSearchPersistenceProviderConformanceShould(OpenSearchPersistenceProviderContainerFixture fixture)
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
	protected override IReadOnlyCollection<Type> RequiredCapabilities =>
		[typeof(IPersistenceProviderHealth)];

	/// <inheritdoc/>
	protected override IPersistenceProvider CreateProvider(string providerName)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"OpenSearch container must be available - real-infra conformance is never skipped.");

		var client = new OpenSearchClient(new ConnectionSettings(_fixture.Endpoint));
		var options = Options.Create(new OpenSearchPersistenceOptions { Name = providerName });

		return new OpenSearchPersistenceProvider(
			client, options, NullLogger<OpenSearchPersistenceProvider>.Instance);
	}

	// -------------------------------------------------------------------------------------------------
	// Conformance arm wiring.
	//
	// The kit ships without test-framework attributes so that a consumer is not forced onto our runner,
	// which means discovery is this suite's job: one member per arm, attributed for xUnit. The kit's
	// ConformanceSuite_ShouldWireEveryArm arm fails if any arm here is missing, so an arm cannot be
	// silently dropped. Every arm is wired unconditionally -- never skipped -- so a genuine conformance
	// gap stays visible in the results instead of being suppressed.
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
