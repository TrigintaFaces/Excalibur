// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing;

/// <summary>
/// Optional <see cref="IEventStore"/> capability that reports the highest event version held for a
/// stream, without loading the stream.
/// </summary>
/// <remarks>
/// <para>
/// This exists to resolve one ambiguity the read surface cannot express. When an aggregate is
/// rehydrated from a snapshot, the events after the snapshot are loaded; an empty result means either
/// that the snapshot is already at the head of the stream -- the normal and common case -- or that the
/// stream stops <em>below</em> the snapshot, so the snapshot describes a state the events can no longer
/// account for. Both return zero rows, and nothing in that result distinguishes them.
/// </para>
/// <para>
/// Implement this only where the answer is cheap -- an indexed maximum over the stream's version
/// column, not a scan of the events themselves. A store that cannot answer cheaply should not
/// implement it: the capability is resolved through
/// <see cref="IServiceProvider.GetService(System.Type)" /> on <see cref="IEventStore"/>, so a store that
/// does not provide it simply is not probed, and the load behaves exactly as it does without this
/// capability. There is no third state and no obligation on any provider.
/// </para>
/// <para>
/// Resolve it through <see cref="IServiceProvider.GetService(System.Type)"/> rather than testing the
/// store's type: a store is commonly reached through a decorator, and a type test reports the
/// decorator's own interface list rather than the capabilities of the store beneath it. A decorator
/// that cannot answer for the whole stream must not forward this capability -- a tiered store whose hot
/// tier has been trimmed would otherwise report a maximum that omits the archived range and make a
/// correctly-archived stream look truncated.
/// </para>
/// </remarks>
public interface IEventStoreVersionProbe
{
	/// <summary>
	/// Gets the highest event version stored for the aggregate stream.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="cancellationToken">Token to observe for cancellation.</param>
	/// <returns>
	/// The highest version present in the stream, or <c>-1</c> when the stream holds no events. Versions
	/// are zero-based, so a stream of <c>n</c> events reports <c>n - 1</c>.
	/// </returns>
	ValueTask<long> GetMaxVersionAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken);
}
