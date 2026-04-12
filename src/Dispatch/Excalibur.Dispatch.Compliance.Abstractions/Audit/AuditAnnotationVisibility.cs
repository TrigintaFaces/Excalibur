// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Controls whether an audit annotation is visible only to its creator or to all authorized users.
/// </summary>
public enum AuditAnnotationVisibility
{
	/// <summary>
	/// Visible only to the actor who created the annotation.
	/// </summary>
	Personal = 0,

	/// <summary>
	/// Visible to all users with sufficient audit log access.
	/// </summary>
	Shared = 1
}
