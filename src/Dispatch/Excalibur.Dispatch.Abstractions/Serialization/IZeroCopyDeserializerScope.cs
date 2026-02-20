// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Represents a scope for efficient batch deserialization with resource reuse.
/// </summary>
public interface IZeroCopyDeserializerScope : IDisposable
{
	/// <summary>
	/// Gets statistics about the scope's resource usage.
	/// </summary>
	ZeroCopyScopeStatistics Statistics { get; }

	/// <summary>
	/// Deserializes an object within the scope, reusing allocated resources.
	/// </summary>
	/// <typeparam name="T"> The type to deserialize to. </typeparam>
	/// <param name="utf8Json"> The UTF-8 JSON data. </param>
	/// <returns> The deserialized object. </returns>
	T? Deserialize<T>(ReadOnlySpan<byte> utf8Json);
}
