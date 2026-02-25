// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Defines the parallelism strategy for executing saga steps.
/// </summary>
public enum ParallelismStrategy
{
	/// <summary>
	/// Execute all steps concurrently with no limit.
	/// </summary>
	Unlimited = 0,

	/// <summary>
	/// Execute steps with a maximum degree of parallelism.
	/// </summary>
	Limited = 1,

	/// <summary>
	/// Execute steps in batches.
	/// </summary>
	Batched = 2,

	/// <summary>
	/// Execute steps with adaptive parallelism based on system resources.
	/// </summary>
	Adaptive = 3,
}

