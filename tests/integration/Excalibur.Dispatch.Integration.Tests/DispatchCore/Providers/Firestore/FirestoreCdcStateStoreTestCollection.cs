// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Firestore;

/// <summary>
/// The single collection every Firestore CDC state-store suite in this assembly belongs to.
/// </summary>
/// <remarks>
/// <para>
/// The fixture builds its client with <c>EmulatorDetection.EmulatorOnly</c>, which can only find its
/// emulator through the <c>FIRESTORE_EMULATOR_HOST</c> PROCESS environment variable. As a class fixture
/// each test class got its own instance, so each started its own container and each overwrote that one
/// variable -- and the class whose write lost then built a client aimed at a container it does not own.
/// Measured as <c>Status(StatusCode="Unavailable" ... target machine actively refused it)</c> the moment a
/// second class was added.
/// </para>
/// <para>
/// A shared collection fixture is one container and one write. Put
/// <c>[Collection(FirestoreCdcStateStoreTestCollection.CollectionName)]</c> on any new Firestore CDC
/// suite; a class with no collection attribute is its own collection and reintroduces the race.
/// </para>
/// </remarks>
[CollectionDefinition(CollectionName)]
public class FirestoreCdcStateStoreTestCollection
	: ICollectionFixture<FirestoreCdcStateStoreContainerFixture>
{
	/// <summary> The collection name every Firestore CDC state-store test class must use. </summary>
	public const string CollectionName = "Firestore CDC State Store Integration Tests";
}
