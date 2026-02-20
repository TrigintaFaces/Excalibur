// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Types of context changes that can be detected.
/// </summary>
public enum ContextChangeType
{
	/// <summary>
	/// A field was added to the context.
	/// </summary>
	Added = 0,

	/// <summary>
	/// A field was removed from the context.
	/// </summary>
	Removed = 1,

	/// <summary>
	/// A field value was modified.
	/// </summary>
	Modified = 2,
}
