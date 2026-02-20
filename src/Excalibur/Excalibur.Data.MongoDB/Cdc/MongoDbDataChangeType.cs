// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Cdc;

namespace Excalibur.Data.MongoDB.Cdc;

/// <summary>
/// Represents the type of data change captured from MongoDB Change Streams.
/// </summary>
/// <remarks>
/// <para>
/// This type is retained for backward compatibility. New code should use
/// <see cref="CdcChangeType"/> from the Excalibur.Cdc package directly,
/// which is the canonical CDC change type shared across all providers.
/// </para>
/// </remarks>
public enum MongoDbDataChangeType
{
	/// <summary>
	/// Unknown or unsupported operation type.
	/// Maps to <see cref="CdcChangeType.None"/>.
	/// </summary>
	Unknown = 0,

	/// <summary>
	/// A document was inserted.
	/// Maps to <see cref="CdcChangeType.Insert"/>.
	/// </summary>
	Insert = 1,

	/// <summary>
	/// A document was updated.
	/// Maps to <see cref="CdcChangeType.Update"/>.
	/// </summary>
	Update = 2,

	/// <summary>
	/// A document was replaced.
	/// Maps to <see cref="CdcChangeType.Replace"/>.
	/// </summary>
	Replace = 3,

	/// <summary>
	/// A document was deleted.
	/// Maps to <see cref="CdcChangeType.Delete"/>.
	/// </summary>
	Delete = 4,

	/// <summary>
	/// A collection or database was dropped, or a rename occurred.
	/// Maps to <see cref="CdcChangeType.Invalidate"/>.
	/// </summary>
	Invalidate = 5,

	/// <summary>
	/// A collection was dropped.
	/// Maps to <see cref="CdcChangeType.Drop"/>.
	/// </summary>
	Drop = 6,

	/// <summary>
	/// A database was dropped.
	/// Maps to <see cref="CdcChangeType.DropDatabase"/>.
	/// </summary>
	DropDatabase = 7,

	/// <summary>
	/// A collection was renamed.
	/// Maps to <see cref="CdcChangeType.Rename"/>.
	/// </summary>
	Rename = 8,
}

/// <summary>
/// Extension methods for converting between <see cref="MongoDbDataChangeType"/> and <see cref="CdcChangeType"/>.
/// </summary>
public static class MongoDbDataChangeTypeExtensions
{
	/// <summary>
	/// Converts a provider-specific <see cref="MongoDbDataChangeType"/> to the canonical <see cref="CdcChangeType"/>.
	/// </summary>
	/// <param name="changeType">The provider-specific change type.</param>
	/// <returns>The canonical <see cref="CdcChangeType"/>.</returns>
	public static CdcChangeType ToCdcChangeType(this MongoDbDataChangeType changeType) =>
		changeType switch
		{
			MongoDbDataChangeType.Insert => CdcChangeType.Insert,
			MongoDbDataChangeType.Update => CdcChangeType.Update,
			MongoDbDataChangeType.Replace => CdcChangeType.Replace,
			MongoDbDataChangeType.Delete => CdcChangeType.Delete,
			MongoDbDataChangeType.Invalidate => CdcChangeType.Invalidate,
			MongoDbDataChangeType.Drop => CdcChangeType.Drop,
			MongoDbDataChangeType.DropDatabase => CdcChangeType.DropDatabase,
			MongoDbDataChangeType.Rename => CdcChangeType.Rename,
			_ => CdcChangeType.None,
		};
}
