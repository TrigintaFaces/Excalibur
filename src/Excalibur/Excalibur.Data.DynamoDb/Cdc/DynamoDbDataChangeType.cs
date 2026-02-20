// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Cdc;

namespace Excalibur.Data.DynamoDb.Cdc;

/// <summary>
/// Specifies the type of change captured from DynamoDB Streams.
/// </summary>
/// <remarks>
/// <para>
/// This type is retained for backward compatibility. New code should use
/// <see cref="CdcChangeType"/> from the Excalibur.Cdc package directly,
/// which is the canonical CDC change type shared across all providers.
/// </para>
/// <para>
/// DynamoDB naming conventions differ from other providers:
/// Modify maps to Update, Remove maps to Delete.
/// </para>
/// </remarks>
public enum DynamoDbDataChangeType
{
	/// <summary>
	/// A new item was inserted into the table.
	/// Maps to <see cref="CdcChangeType.Insert"/>.
	/// </summary>
	Insert,

	/// <summary>
	/// An existing item was modified.
	/// Maps to <see cref="CdcChangeType.Update"/>.
	/// </summary>
	Modify,

	/// <summary>
	/// An item was removed from the table.
	/// Maps to <see cref="CdcChangeType.Delete"/>.
	/// </summary>
	Remove,
}

/// <summary>
/// Extension methods for converting between <see cref="DynamoDbDataChangeType"/> and <see cref="CdcChangeType"/>.
/// </summary>
public static class DynamoDbDataChangeTypeExtensions
{
	/// <summary>
	/// Converts a provider-specific <see cref="DynamoDbDataChangeType"/> to the canonical <see cref="CdcChangeType"/>.
	/// </summary>
	/// <param name="changeType">The provider-specific change type.</param>
	/// <returns>The canonical <see cref="CdcChangeType"/>.</returns>
	public static CdcChangeType ToCdcChangeType(this DynamoDbDataChangeType changeType) =>
		changeType switch
		{
			DynamoDbDataChangeType.Insert => CdcChangeType.Insert,
			DynamoDbDataChangeType.Modify => CdcChangeType.Update,
			DynamoDbDataChangeType.Remove => CdcChangeType.Delete,
			_ => CdcChangeType.None,
		};
}
