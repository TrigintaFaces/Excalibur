// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance;

/// <summary>
/// Classifies the kind of annotation attached to an audit event.
/// </summary>
public enum AuditAnnotationType
{
	/// <summary>
	/// A string label applied to an audit event for categorization.
	/// </summary>
	Tag = 0,

	/// <summary>
	/// A flag marking an event for quick retrieval, optionally with a label.
	/// </summary>
	Bookmark = 1,

	/// <summary>
	/// A free-text note attached to an audit event with actor attribution.
	/// </summary>
	Note = 2
}
