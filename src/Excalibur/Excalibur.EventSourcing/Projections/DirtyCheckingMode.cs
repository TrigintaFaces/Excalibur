// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Controls how projection dirty checking is performed before persisting state.
/// </summary>
public enum DirtyCheckingMode
{
	/// <summary>
	/// No dirty checking -- always persist after handler invocation (default).
	/// </summary>
	None = 0,

	/// <summary>
	/// Use <see cref="object.Equals(object?)"/> to compare before/after state.
	/// Ideal for records (value semantics) and immutable projections.
	/// </summary>
	Equality = 1,

	/// <summary>
	/// Use reference equality -- skip persist if the handler returned the same object reference.
	/// Only meaningful for immutable projections that return a new instance on change.
	/// </summary>
	ReferenceEquality = 2,
}
