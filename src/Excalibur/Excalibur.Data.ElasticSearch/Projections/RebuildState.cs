// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents the state of a rebuild operation.
/// </summary>
public enum RebuildState
{
	/// <summary>
	/// The rebuild is queued but not yet started.
	/// </summary>
	Queued = 0,

	/// <summary>
	/// The rebuild is currently in progress.
	/// </summary>
	InProgress = 1,

	/// <summary>
	/// The rebuild is paused.
	/// </summary>
	Paused = 2,

	/// <summary>
	/// The rebuild completed successfully.
	/// </summary>
	Completed = 3,

	/// <summary>
	/// The rebuild failed with errors.
	/// </summary>
	Failed = 4,

	/// <summary>
	/// The rebuild was cancelled by user request.
	/// </summary>
	Cancelled = 5,
}
