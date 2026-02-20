// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Cdc;

namespace Excalibur.Data.CosmosDb.Cdc;

/// <summary>
/// Represents the type of change captured from CosmosDb Change Feed.
/// </summary>
/// <remarks>
/// <para>
/// This type is retained for backward compatibility. New code should use
/// <see cref="CdcChangeType"/> from the Excalibur.Cdc package directly,
/// which is the canonical CDC change type shared across all providers.
/// </para>
/// </remarks>
public enum CosmosDbDataChangeType
{
	/// <summary>
	/// A new document was inserted.
	/// Maps to <see cref="CdcChangeType.Insert"/>.
	/// </summary>
	Insert,

	/// <summary>
	/// An existing document was updated.
	/// Maps to <see cref="CdcChangeType.Update"/>.
	/// </summary>
	Update,

	/// <summary>
	/// A document was deleted.
	/// Maps to <see cref="CdcChangeType.Delete"/>.
	/// </summary>
	/// <remarks>
	/// Delete events are only available with AllVersionsAndDeletes mode,
	/// which requires the container to have changeFeedPolicy configured.
	/// </remarks>
	Delete,
}

/// <summary>
/// Extension methods for converting between <see cref="CosmosDbDataChangeType"/> and <see cref="CdcChangeType"/>.
/// </summary>
public static class CosmosDbDataChangeTypeExtensions
{
	/// <summary>
	/// Converts a provider-specific <see cref="CosmosDbDataChangeType"/> to the canonical <see cref="CdcChangeType"/>.
	/// </summary>
	/// <param name="changeType">The provider-specific change type.</param>
	/// <returns>The canonical <see cref="CdcChangeType"/>.</returns>
	public static CdcChangeType ToCdcChangeType(this CosmosDbDataChangeType changeType) =>
		changeType switch
		{
			CosmosDbDataChangeType.Insert => CdcChangeType.Insert,
			CosmosDbDataChangeType.Update => CdcChangeType.Update,
			CosmosDbDataChangeType.Delete => CdcChangeType.Delete,
			_ => CdcChangeType.None,
		};
}
