// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Jitter strategies for retry delays.
/// </summary>
public enum JitterStrategy
{
	/// <summary>
	/// No jitter applied.
	/// </summary>
	None = 0,

	/// <summary>
	/// Full jitter: random between 0 and calculated delay.
	/// </summary>
	Full = 1,

	/// <summary>
	/// Equal jitter: half delay plus random between 0 and half delay.
	/// </summary>
	Equal = 2,

	/// <summary>
	/// Decorrelated jitter: increases randomness with each attempt.
	/// </summary>
	Decorrelated = 3,

	/// <summary>
	/// Exponential jitter: exponentially increasing jitter range.
	/// </summary>
	Exponential = 4,
}
