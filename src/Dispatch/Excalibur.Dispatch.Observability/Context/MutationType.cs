// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Types of mutations that can occur.
/// </summary>
internal enum MutationType
{
	/// <summary>
	/// An item was added.
	/// </summary>
	Added = 0,

	/// <summary>
	/// An item was removed.
	/// </summary>
	Removed = 1,

	/// <summary>
	/// An item was modified.
	/// </summary>
	Modified = 2,
}
