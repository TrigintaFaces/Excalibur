// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Middleware.ParallelExecution;

/// <summary>
/// Defines the strategy for handling failures during parallel event handler execution.
/// </summary>
public enum WhenAllStrategy
{
	/// <summary>
	/// Wait for all handlers to complete, even if some fail.
	/// All exceptions are collected into an <see cref="AggregateException"/>.
	/// </summary>
	WaitAll = 0,

	/// <summary>
	/// Cancel remaining handlers on first failure.
	/// The first exception is immediately propagated.
	/// </summary>
	FirstFailure = 1,
}
