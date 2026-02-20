// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Instrumented channel reader.
/// </summary>
internal sealed class InstrumentedChannelReader<T>(ChannelReader<T> innerReader, InternalChannelMetrics metrics) : ChannelReader<T>
{
	/// <inheritdoc/>
	public override bool CanCount => innerReader.CanCount;

	/// <inheritdoc/>
	public override int Count => innerReader.Count;

	/// <inheritdoc/>
	public override Task Completion => innerReader.Completion;

	/// <inheritdoc/>
	public override bool TryRead(out T item)
	{
		var result = innerReader.TryRead(out item!);
		if (result)
		{
			metrics.RecordRead();
		}

		return result;
	}

	/// <inheritdoc/>
	public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken) =>
		innerReader.WaitToReadAsync(cancellationToken);

	/// <inheritdoc/>
	public override bool TryPeek([MaybeNullWhen(false)] out T item) => innerReader.TryPeek(out item);
}
