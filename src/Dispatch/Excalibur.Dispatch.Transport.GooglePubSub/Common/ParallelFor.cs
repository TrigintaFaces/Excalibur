// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Parallel processing utilities.
/// </summary>
public static class ParallelFor
{
	/// <summary>
	/// Executes a for loop in parallel.
	/// </summary>
	/// <param name="fromInclusive"> The start index (inclusive). </param>
	/// <param name="toExclusive"> The end index (exclusive). </param>
	/// <param name="body"> The loop body. </param>
	public static void Each(int fromInclusive, int toExclusive, Action<int> body) => _ = Parallel.For(fromInclusive, toExclusive, body);
}
