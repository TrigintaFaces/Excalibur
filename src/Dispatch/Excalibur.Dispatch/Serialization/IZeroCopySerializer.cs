// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.IO.Pipelines;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Zero-copy serializer using System.IO.Pipelines. Implements R9.7 (zero-copy) and R9.17-R9.18 (System.IO.Pipelines).
/// </summary>
public interface IZeroCopySerializer
{
	/// <summary>
	/// Serializes a message to a pipe writer without allocations.
	/// </summary>
	ValueTask SerializeAsync<T>(PipeWriter writer, T message, CancellationToken cancellationToken);

	/// <summary>
	/// Deserializes a message from a pipe reader without allocations.
	/// </summary>
	ValueTask<T> DeserializeAsync<T>(PipeReader reader, CancellationToken cancellationToken);

	/// <summary>
	/// Serializes directly to a memory buffer.
	/// </summary>
	int Serialize<T>(T message, Memory<byte> buffer);

	/// <summary>
	/// Deserializes directly from a memory buffer.
	/// </summary>
	T Deserialize<T>(ReadOnlyMemory<byte> buffer);
}
