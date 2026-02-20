// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Batch completion strategies.
/// </summary>
public enum BatchCompletionStrategy
{
	/// <summary>
	/// Complete batch when size limit is reached.
	/// </summary>
	Size = 0,

	/// <summary>
	/// Complete batch when time limit is reached.
	/// </summary>
	Time = 1,

	/// <summary>
	/// Complete batch when either size or time limit is reached.
	/// </summary>
	SizeOrTime = 2,

	/// <summary>
	/// Complete batch based on dynamic conditions.
	/// </summary>
	Dynamic = 3,

	/// <summary>
	/// Complete batch based on message content.
	/// </summary>
	ContentBased = 4,
}
