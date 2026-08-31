// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Excalibur.Saga.Firestore;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Conformance.Saga;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Optimistic-concurrency conformance for the Firestore saga store (e1tsq2, S853) — one of the five
/// distributed providers. Author≠impl (TestsDeveloper); runs the shared
/// <see cref="SagaStoreConformanceTestBase"/> contract with <see cref="SupportsOptimisticConcurrency"/>
/// enabled against the Firestore emulator.
/// </summary>
/// <remarks>
/// RED on the pre-fix read-then-<c>SetAsync</c> with no transaction/precondition (the XML doc claimed
/// transactions it didn't use); GREEN on the e1tsq2 <c>RunTransactionAsync</c> version-gated write
/// (mismatch → <see cref="ConcurrencyException"/>) + no-resurrect guard. A fresh collection per test gives
/// isolation on the shared emulator. Verifies at CRUCIBLE / full-CI (the fixture degrades gracefully when
/// the emulator can't start).
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "Firestore")]
[Collection(global::Excalibur.Integration.Tests.FirestoreSerialCollection.CollectionName)]
public sealed class FirestoreSagaStoreConcurrencyConformanceShould : SagaStoreConformanceTestBase
{
	private readonly FirestoreSagaStoreContainerFixture _fixture;
	private readonly string _collectionName = $"sagas_{Guid.NewGuid():N}";

	public FirestoreSagaStoreConcurrencyConformanceShould(FirestoreSagaStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override bool SupportsOptimisticConcurrency => true;

	// FirestoreSagaStore DOES implement ISagaStore.PurgeCompletedBeforeAsync. This was declared false back
	// when the store fell through to the interface default that throws NotSupportedException; the store has
	// since grown a real implementation, and the declaration went stale rather than wrong-at-the-time.
	//
	// Leaving it false is not the harmless side of the mistake: the base gate then asserts the store THROWS,
	// so the arm demanded the absence of a feature that exists, and would have kept passing if the working
	// implementation were deleted.
	//
	// The store refuses only a tenant-SCOPED purge, because it persists saga state as a serialized document
	// and cannot build a tenant predicate -- servicing that call would delete every other tenant's completed
	// sagas. This suite runs unscoped, which is the path the store supports.
	/// <inheritdoc/>
	protected override bool SupportsCompletedPurge => true;

	/// <inheritdoc/>
	protected override Task<ISagaStore> CreateStoreAsync()
	{
		var options = Options.Create(new FirestoreSagaOptions
		{
			ProjectId = _fixture.ProjectId,
			CollectionName = _collectionName,
			EmulatorHost = _fixture.EmulatorEndpoint,
		});

		return Task.FromResult<ISagaStore>(
			new FirestoreSagaStore(
				_fixture.Db, options, NullLogger<FirestoreSagaStore>.Instance, new DispatchJsonSerializer(),
				SingleTenantTestContext.Instance));
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync() => Task.CompletedTask; // throwaway per-test collection; emulator disposed at end
}
