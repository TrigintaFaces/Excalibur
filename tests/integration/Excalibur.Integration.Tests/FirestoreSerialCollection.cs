// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Integration.Tests.Data.EventStore;
using Excalibur.Integration.Tests.Data.Inbox;
using Excalibur.Integration.Tests.Data.Outbox;
using Excalibur.Integration.Tests.Data.Persistence;
using Excalibur.Integration.Tests.Data.Saga;
using Excalibur.Integration.Tests.Data.Snapshots;

namespace Excalibur.Integration.Tests;

/// <summary>
/// The single collection every Firestore-touching test belongs to.
/// </summary>
/// <remarks>
/// <para>
/// Firestore's <c>EmulatorDetection.EmulatorOnly</c> can only discover its emulator through the
/// <c>FIRESTORE_EMULATOR_HOST</c> <em>process</em> environment variable; the client builder exposes no
/// explicit-endpoint escape hatch. Process environment is global, so two Firestore fixtures that run at
/// the same time overwrite each other's endpoint and one suite silently talks to the other's emulator.
/// </para>
/// <para>
/// That is not hypothetical. With these suites split across five separate collections, running the whole
/// Firestore surface produced 22 failures out of 224 while every one of those classes passed alone. The
/// failures moved between runs depending on which pair happened to overlap, which is what a race on
/// shared global state looks like from the outside.
/// </para>
/// <para>
/// <b>Configuration cannot fix this and both knobs have been measured inert.</b> The project's
/// <c>xunit.runner.json</c> sets <c>parallelizeTestCollections: false</c> and <c>maxParallelThreads: 1</c>,
/// is well-formed, and is copied to the output directory — and collections still overlap. Adding
/// <c>[assembly: CollectionBehavior(DisableTestParallelization = true)]</c> changed nothing either. A
/// committed probe measured two collections starting in the same millisecond in both cases.
/// </para>
/// <para>
/// What does hold is xUnit's own collection semantics: tests in ONE collection never run concurrently.
/// That is a property of the runner's scheduling model rather than a setting it may or may not honour,
/// which is why the fix is this type rather than another configuration file.
/// </para>
/// <para>
/// <b>Adding a Firestore suite:</b> put <c>[Collection(FirestoreSerialCollection.CollectionName)]</c> on it.
/// A class with no collection attribute is its own collection and will run in parallel with this one,
/// which reintroduces the race. <c>Diagnostics/CollectionParallelismProbeShould.cs</c> is the guard.
/// </para>
/// </remarks>
[CollectionDefinition(CollectionName)]
public class FirestoreSerialCollection
	: ICollectionFixture<FirestoreEventStoreContainerFixture>,
		ICollectionFixture<FirestoreInboxStoreContainerFixture>,
		ICollectionFixture<FirestoreOutboxStoreContainerFixture>,
		ICollectionFixture<FirestorePersistenceProviderContainerFixture>,
		ICollectionFixture<FirestoreSagaStoreContainerFixture>,
		ICollectionFixture<FirestoreSnapshotStoreContainerFixture>
{
	/// <summary> The collection name every Firestore-touching test class must use. </summary>
	public const string CollectionName = "Firestore Integration Tests";
}
