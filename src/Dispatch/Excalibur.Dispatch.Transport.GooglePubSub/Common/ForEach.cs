// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Parallel foreach utilities.
/// </summary>
public static class ForEach
{
	/// <summary>
	/// Executes a foreach loop in parallel.
	/// </summary>
	/// <typeparam name="T"> The type of items. </typeparam>
	/// <param name="source"> The source collection. </param>
	/// <param name="body"> The loop body. </param>
	public static void Execute<T>(IEnumerable<T> source, Action<T> body) => _ = Parallel.ForEach(source, body);
}
