// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Minimal extensions for in-memory message envelopes.
/// </summary>
public static class MemoryMessageEnvelopeExtensions
{
	/// <summary>
	/// Returns true if the instance is not null.
	/// </summary>
	/// <typeparam name="T"> The instance type. </typeparam>
	/// <param name="value"> The value to test. </param>
	/// <returns> True when value is not null; otherwise false. </returns>
	public static bool IsNotNull<T>(this T? value)
		where T : class
		=> value is not null;
}
