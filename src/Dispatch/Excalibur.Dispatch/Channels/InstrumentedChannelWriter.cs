// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Threading.Channels;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Instrumented channel writer.
/// </summary>
internal sealed class InstrumentedChannelWriter<T>(ChannelWriter<T> innerWriter, InternalChannelMetrics metrics) : ChannelWriter<T>
{
	/// <inheritdoc/>
	public override bool TryWrite(T item)
	{
		var result = innerWriter.TryWrite(item);
		if (result)
		{
			metrics.RecordWrite();
		}

		return result;
	}

	/// <inheritdoc/>
	public override ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken) =>
		innerWriter.WaitToWriteAsync(cancellationToken);

	/// <inheritdoc/>
	public override ValueTask WriteAsync(T item, CancellationToken cancellationToken)
	{
		metrics.RecordWrite();
		return innerWriter.WriteAsync(item, cancellationToken);
	}

	/// <inheritdoc/>
	public override bool TryComplete(Exception? error = null) => innerWriter.TryComplete(error);
}
