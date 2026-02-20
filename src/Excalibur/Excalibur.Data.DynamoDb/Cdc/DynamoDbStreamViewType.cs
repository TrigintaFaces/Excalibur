// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.DynamoDb.Cdc;

/// <summary>
/// Specifies the information written to DynamoDB Streams.
/// </summary>
public enum DynamoDbStreamViewType
{
	/// <summary>
	/// Only the key attributes of the modified item.
	/// </summary>
	KeysOnly,

	/// <summary>
	/// The entire item as it appears after it was modified.
	/// </summary>
	NewImage,

	/// <summary>
	/// The entire item as it appeared before it was modified.
	/// </summary>
	OldImage,

	/// <summary>
	/// Both the new and old item images.
	/// </summary>
	/// <remarks>
	/// This is the recommended setting for CDC scenarios as it provides
	/// complete before/after state for change tracking.
	/// </remarks>
	NewAndOldImages,
}
