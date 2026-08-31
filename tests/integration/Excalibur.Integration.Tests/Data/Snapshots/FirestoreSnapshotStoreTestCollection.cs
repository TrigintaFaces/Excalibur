// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// xUnit collection definition for Firestore SnapshotStore integration tests.
/// Collection definitions must be in the same assembly as the tests.
/// </summary>
public static class FirestoreSnapshotStoreTestCollection
{
	/// <summary>
	/// The collection name used by test classes.
	/// </summary>
	public const string CollectionName = global::Excalibur.Integration.Tests.FirestoreSerialCollection.CollectionName;
}
