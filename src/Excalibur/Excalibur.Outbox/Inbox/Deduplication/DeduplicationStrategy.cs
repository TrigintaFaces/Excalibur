// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Defines the strategy for message deduplication.
/// </summary>
public enum DeduplicationStrategy
{
	/// <summary>
	/// Use message ID for deduplication.
	/// </summary>
	MessageId = 0,

	/// <summary>
	/// Use content hash for deduplication.
	/// </summary>
	ContentHash = 1,

	/// <summary>
	/// Use both message ID and content hash.
	/// </summary>
	Composite = 2,
}
