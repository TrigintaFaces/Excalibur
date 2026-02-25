// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Represents the current state of a projection rebuild operation.
/// </summary>
public enum ProjectionRebuildState
{
	/// <summary>
	/// No rebuild is in progress.
	/// </summary>
	Idle,

	/// <summary>
	/// A rebuild is currently in progress.
	/// </summary>
	Rebuilding,

	/// <summary>
	/// The rebuild has completed successfully.
	/// </summary>
	Completed,

	/// <summary>
	/// The rebuild has failed.
	/// </summary>
	Failed
}
