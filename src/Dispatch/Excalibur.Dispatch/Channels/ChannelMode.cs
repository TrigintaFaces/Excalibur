// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Channel mode enumeration.
/// </summary>
public enum ChannelMode
{
	/// <summary>
	/// Unbounded channel with no capacity limit.
	/// </summary>
	Unbounded = 0,

	/// <summary>
	/// Bounded channel with a capacity limit.
	/// </summary>
	Bounded = 1,
}
