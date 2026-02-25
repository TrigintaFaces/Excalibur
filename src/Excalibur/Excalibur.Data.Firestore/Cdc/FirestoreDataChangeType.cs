// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Cdc;

namespace Excalibur.Data.Firestore.Cdc;

/// <summary>
/// Represents the type of change captured from Firestore Realtime Listeners.
/// </summary>
/// <remarks>
/// <para>
/// This type is retained for backward compatibility. New code should use
/// <see cref="CdcChangeType"/> from the Excalibur.Cdc package directly,
/// which is the canonical CDC change type shared across all providers.
/// </para>
/// <para>
/// Firestore naming conventions differ from other providers:
/// Added maps to Insert, Modified maps to Update, Removed maps to Delete.
/// </para>
/// </remarks>
public enum FirestoreDataChangeType
{
	/// <summary>
	/// A new document was added to the collection.
	/// Maps to <see cref="CdcChangeType.Insert"/>.
	/// </summary>
	Added,

	/// <summary>
	/// An existing document was modified.
	/// Maps to <see cref="CdcChangeType.Update"/>.
	/// </summary>
	Modified,

	/// <summary>
	/// A document was removed from the collection.
	/// Maps to <see cref="CdcChangeType.Delete"/>.
	/// </summary>
	Removed,
}

/// <summary>
/// Extension methods for converting between <see cref="FirestoreDataChangeType"/> and <see cref="CdcChangeType"/>.
/// </summary>
public static class FirestoreDataChangeTypeExtensions
{
	/// <summary>
	/// Converts a provider-specific <see cref="FirestoreDataChangeType"/> to the canonical <see cref="CdcChangeType"/>.
	/// </summary>
	/// <param name="changeType">The provider-specific change type.</param>
	/// <returns>The canonical <see cref="CdcChangeType"/>.</returns>
	public static CdcChangeType ToCdcChangeType(this FirestoreDataChangeType changeType) =>
		changeType switch
		{
			FirestoreDataChangeType.Added => CdcChangeType.Insert,
			FirestoreDataChangeType.Modified => CdcChangeType.Update,
			FirestoreDataChangeType.Removed => CdcChangeType.Delete,
			_ => CdcChangeType.None,
		};
}
