// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Cdc;

namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Represents the type of data change captured during Change Data Capture (CDC) processing.
/// </summary>
/// <remarks>
/// <para>
/// This type is retained for backward compatibility. New code should use
/// <see cref="CdcChangeType"/> from the Excalibur.Cdc package directly,
/// which is the canonical CDC change type shared across all providers.
/// </para>
/// </remarks>
public enum DataChangeType
{
	/// <summary>
	/// The data change type is unknown or not specified.
	/// Maps to <see cref="CdcChangeType.None"/>.
	/// </summary>
	Unknown = 0,

	/// <summary>
	/// The change represents an insert operation.
	/// Maps to <see cref="CdcChangeType.Insert"/>.
	/// </summary>
	Insert = 1,

	/// <summary>
	/// The change represents an update operation.
	/// Maps to <see cref="CdcChangeType.Update"/>.
	/// </summary>
	Update = 2,

	/// <summary>
	/// The change represents a delete operation.
	/// Maps to <see cref="CdcChangeType.Delete"/>.
	/// </summary>
	Delete = 3,
}

/// <summary>
/// Extension methods for converting between <see cref="DataChangeType"/> and <see cref="CdcChangeType"/>.
/// </summary>
public static class DataChangeTypeExtensions
{
	/// <summary>
	/// Converts a provider-specific <see cref="DataChangeType"/> to the canonical <see cref="CdcChangeType"/>.
	/// </summary>
	/// <param name="changeType">The provider-specific change type.</param>
	/// <returns>The canonical <see cref="CdcChangeType"/>.</returns>
	public static CdcChangeType ToCdcChangeType(this DataChangeType changeType) =>
		changeType switch
		{
			DataChangeType.Insert => CdcChangeType.Insert,
			DataChangeType.Update => CdcChangeType.Update,
			DataChangeType.Delete => CdcChangeType.Delete,
			_ => CdcChangeType.None,
		};
}
