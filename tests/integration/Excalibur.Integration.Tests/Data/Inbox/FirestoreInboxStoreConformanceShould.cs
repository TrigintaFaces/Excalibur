// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.Firestore;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Inbox;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Conformance tests for <see cref="FirestoreInboxStore"/> using the Inbox Conformance Test Kit
/// against a real Firestore emulator.
/// </summary>
/// <remarks>
/// These tests verify that the Firestore implementation correctly implements the IInboxStore contract
/// against real infrastructure (the Firestore emulator via TestContainers), exercising the emulator-
/// connected <see cref="Google.Cloud.Firestore.FirestoreDb"/> built with the SDK's default serializer
/// settings.
/// </remarks>
[Collection(FirestoreInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Firestore")]
public sealed class FirestoreInboxStoreConformanceShould : InboxStoreConformanceTestBase
{
	private readonly FirestoreInboxStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreInboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Firestore container fixture.</param>
	public FirestoreInboxStoreConformanceShould(FirestoreInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override Task<IInboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Firestore emulator must be available for real-infrastructure conformance — never skipped.");

		var options = Options.Create(new FirestoreInboxOptions
		{
			ProjectId = _fixture.ProjectId,
			CollectionName = _fixture.CollectionName,
		});

		var store = new FirestoreInboxStore(
			_fixture.Db,
			options,
			NullLogger<FirestoreInboxStore>.Instance,
			SingleTenantTestContext.Instance);

		return Task.FromResult<IInboxStore>(store);
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		await _fixture.CleanupCollectionAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// DECLARED EXEMPTION (47ruyr), not a skip: unlike <c>InMemoryInboxStoreConformanceShould</c>'s
	/// exemption (that store has no external persistence layer to fault at all), Firestore HAS one but we
	/// cannot currently fault it. The .NET client runs <c>EmulatorOnly</c>, which is an admin client that
	/// bypasses security rules entirely, so no rules-based fault is observable; Firestore has no
	/// rename/permission-revoke primitive at the collection/document level; and killing the emulator
	/// container (measured: <c>StopAsync</c> then <c>StartAsync</c>, 30s poll) did not restore a reachable
	/// gRPC channel within a workable test timeout. Tracked, not silently true: ARCHITECTURE.md's D0 row
	/// records Firestore inbox durability as UNVERIFIED, and 47ruyr carries the next approach (a severable
	/// TCP proxy in front of the container).
	/// </summary>
	public override Task ThrowNotNoOpOnPersistenceFailure()
	{
		Assert.Skip(
			"[capability-not-applicable] Firestore cannot wire a real durability fault here: the .NET client "
			+ "authenticates as emulator admin and bypasses security rules, Firestore offers no rename or "
			+ "permission-revoke primitive, and killing the emulator container did not reliably restore "
			+ "connectivity within a workable timeout. Reported SKIPPED rather than passed: this provider "
			+ "verified nothing, and a run that verified nothing must not report what a run that verified "
			+ "everything reports. The gap is recorded by name in Excalibur.Inbox/ARCHITECTURE.md's D0 row.");
		return Task.CompletedTask;
	}
}
