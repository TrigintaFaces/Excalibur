// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Specifies the failure handling policy for parallel saga step execution.
/// </summary>
/// <remarks>
/// Replaces the previous <c>RequireAllSuccess</c> and <c>ContinueOnFailure</c> boolean
/// properties to eliminate the boolean trap and make failure semantics explicit.
/// </remarks>
public enum ParallelFailurePolicy
{
	/// <summary>
	/// Cancel remaining steps immediately on first failure.
	/// </summary>
	FailFast = 0,

	/// <summary>
	/// Continue executing remaining steps even if one fails.
	/// The aggregated result includes both successes and failures.
	/// </summary>
	ContinueOnFailure = 1,

	/// <summary>
	/// All steps must succeed; the step fails if any child step fails.
	/// </summary>
	RequireAll = 2,
}
