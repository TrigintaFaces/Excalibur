// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// xUnit collection definition for the Firestore outbox integration tests.
/// </summary>
/// <remarks>
/// A class fixture would give each of these classes its own emulator, so two heavyweight containers
/// start where one will do and the fixture's own client — which still resolves through the process-wide
/// <c>FIRESTORE_EMULATOR_HOST</c> variable — has a second writer competing with it. Sharing the fixture
/// keeps that to one container and one write. The stores themselves no longer read that variable; they
/// take an explicit endpoint, which is what fixed the connection failures this collection was first
/// written for.
/// </remarks>
public static class FirestoreOutboxTestCollection
{
	/// <summary>
	/// The collection name used by test classes.
	/// </summary>
	public const string CollectionName = global::Excalibur.Integration.Tests.FirestoreSerialCollection.CollectionName;
}
