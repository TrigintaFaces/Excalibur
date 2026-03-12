// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.IO.Pipelines;
using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Extension methods for <see cref="ISerializer"/> providing PipeWriter/PipeReader overloads.
/// </summary>
/// <remarks>
/// These extensions require the System.IO.Pipelines dependency and are therefore in the
/// Excalibur.Dispatch package rather than Abstractions.
/// </remarks>
public static class SerializerPipeExtensions
{
	/// <summary>
	/// Serializes a value to a <see cref="PipeWriter"/> asynchronously.
	/// </summary>
	/// <typeparam name="T">The type to serialize.</typeparam>
	/// <param name="serializer">The serializer.</param>
	/// <param name="pipeWriter">The pipe writer to write to.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public static async ValueTask SerializeAsync<T>(
		this ISerializer serializer,
		PipeWriter pipeWriter,
		T value,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(pipeWriter);

		cancellationToken.ThrowIfCancellationRequested();

		serializer.Serialize(value, pipeWriter);
		await pipeWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Deserializes a value from a <see cref="PipeReader"/> asynchronously.
	/// </summary>
	/// <typeparam name="T">The type to deserialize to.</typeparam>
	/// <param name="serializer">The serializer.</param>
	/// <param name="pipeReader">The pipe reader to read from.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	public static async ValueTask<T> DeserializeAsync<T>(
		this ISerializer serializer,
		PipeReader pipeReader,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(pipeReader);

		var result = await pipeReader.ReadAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			return serializer.Deserialize<T>(result.Buffer);
		}
		finally
		{
			pipeReader.AdvanceTo(result.Buffer.End);
		}
	}
}
