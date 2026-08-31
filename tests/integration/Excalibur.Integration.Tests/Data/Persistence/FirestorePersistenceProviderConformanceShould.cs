// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore;
using Excalibur.Data.Persistence;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Persistence;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="FirestorePersistenceProvider"/> using the
/// Persistence Provider Conformance Test Kit against a live Firestore emulator.
/// </summary>
/// <remarks>
/// <para>
/// Never skipped: when Docker is unavailable the fixture fails fast, so a missing emulator surfaces as a
/// failure rather than a silent pass. The provider is constructed through its consumer-supplied-client
/// constructor with the fixture's emulator-connected <c>FirestoreDb</c>.
/// </para>
/// <para>
/// The provider declines <c>IPersistenceProviderTransaction</c>: <c>GetService</c> answers
/// <see langword="null"/> for it and the provider no longer implements it, so the three scope arms
/// return without asserting. That is the documented way to decline, and it is honest here -- Firestore
/// has no ambient, open-ended transaction scope to give. The capabilities it genuinely does offer,
/// <c>IPersistenceProviderHealth</c> and <c>IPersistenceProviderConnection</c>, are declared in
/// <c>RequiredCapabilities</c>, so an implementation that quietly stopped offering either fails loudly
/// rather than emptying those arms into a green report.
/// </para>
/// </remarks>
[Collection(FirestorePersistenceProviderTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Firestore")]
[Trait("Pattern", "PROVIDER")]
public sealed class FirestorePersistenceProviderConformanceShould
	: PersistenceProviderConformanceTestKit
{
	private readonly FirestorePersistenceProviderContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestorePersistenceProviderConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Firestore emulator container fixture.</param>
	public FirestorePersistenceProviderConformanceShould(FirestorePersistenceProviderContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override string ExpectedProviderType => "CloudNative";

	/// <inheritdoc/>
	protected override IReadOnlyCollection<Type> RequiredCapabilities =>
		[typeof(IPersistenceProviderHealth), typeof(IPersistenceProviderConnection)];

	/// <inheritdoc/>
	protected override IPersistenceProvider CreateProvider(string providerName)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Firestore emulator container must be available - real-infra conformance is never skipped.");

		var options = Options.Create(new FirestoreOptions
		{
			Name = providerName,
			ProjectId = _fixture.ProjectId,
			EmulatorHost = _fixture.EmulatorEndpoint,
			DefaultCollection = "persistence_conformance",
		});

		// The options-only constructor, which is the one DI resolves: nothing in src registers a
		// FirestoreDb, so the database-taking overload used here previously is a shape no consumer can
		// reach. That overload also arrives pre-initialized, so this suite could not have observed a
		// provider that never initializes. Built from options, every arm below now runs through the
		// provider's own initialization against the real emulator.
		return new FirestorePersistenceProvider(
			options, NullLogger<FirestorePersistenceProvider>.Instance);
	}

	// -------------------------------------------------------------------------------------------------
	// Conformance arm wiring.
	//
	// The kit ships without test-framework attributes so that a consumer is not forced onto our runner,
	// which means discovery is this suite's job: one member per arm, attributed for xUnit. The kit's
	// ConformanceSuite_ShouldWireEveryArm arm fails if any arm here is missing, so an arm cannot be
	// silently dropped. Every arm is wired unconditionally -- including the three transaction arms this
	// provider declines -- because a skipped arm is indistinguishable from a passing one in the
	// results, and a decline must stay visible rather than look like a verification.
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
